using System;
using UnityEngine;

namespace CricketVR
{
    public static class Units
    {
        public static float KmhToMs(float kmh) => kmh / 3.6f;
        public static float MsToKmh(float ms) => ms * 3.6f;
    }

    /// <summary>
    /// One delivery request. Release position, speed, line and length are independent fields (CLAUDE.md §9).
    /// Line and length describe the requested FIRST BOUNCE point: x = line, z = length (metres in front of the
    /// striker's stumps, which stand at z = 0).
    /// </summary>
    [Serializable]
    public struct DeliveryConfig
    {
        public string label;
        [Tooltip("Release speed in km/h.")]
        public float speedKmh;
        [Tooltip("Ball centre at release (world, metres).")]
        public Vector3 releasePosition;
        [Tooltip("Bounce x (m). 0 = middle stump; + = off side for a right-hander.")]
        public float line;
        [Tooltip("Bounce distance in front of the striker's stumps (m).")]
        public float length;

        public static DeliveryConfig Create(string label, float speedKmh, float line, float length) => new DeliveryConfig
        {
            label = label,
            speedKmh = speedKmh,
            releasePosition = FieldDimensions.DefaultReleasePoint,
            line = line,
            length = length,
        };

        public override string ToString() => $"{label}: {speedKmh:F0} km/h, line {line:+0.00;-0.00;0.00} m, length {length:F2} m";
    }

    public struct DeliverySolution
    {
        public bool Reachable;
        /// <summary>Initial ball velocity (m/s, world). Magnitude equals the requested speed.</summary>
        public Vector3 Velocity;
        /// <summary>Requested bounce point (ball centre at contact height).</summary>
        public Vector3 Target;
        /// <summary>Where the simulated (discrete-step) trajectory reaches contact height.</summary>
        public Vector3 PredictedBounce;
        public float ElevationDegrees;
        /// <summary>Lowest speed (km/h) that can reach the target, for error messages.</summary>
        public float MinimumSpeedKmh;
        public string Error;
    }

    /// <summary>
    /// Computes the release velocity that lands the ball on a requested bounce point at a fixed speed, under gravity
    /// with no air drag (MVP decision). The speed is never changed: an unreachable request is reported, not adjusted.
    /// </summary>
    public static class DeliverySolver
    {
        const int MaxRefineIterations = 30;
        const float RefineTolerance = 0.005f; // metres along the ground

        /// <param name="gravity">Downward acceleration magnitude (m/s², positive).</param>
        /// <param name="fixedDeltaTime">Physics step; used to correct for the step-wise integration (0 = skip).</param>
        /// <param name="ballRadius">The bounce happens when the ball centre is this high above the ground.</param>
        public static DeliverySolution Solve(DeliveryConfig config, float gravity, float fixedDeltaTime, float ballRadius)
        {
            var result = new DeliverySolution
            {
                Target = new Vector3(config.line, ballRadius, config.length),
            };
            float speed = Units.KmhToMs(config.speedKmh);

            if (!(speed > 0f) || !float.IsFinite(speed) || !(gravity > 0f))
                return Fail(result, $"invalid speed {config.speedKmh} km/h or gravity {gravity}");

            var release = config.releasePosition;
            var toTarget = result.Target - release;
            var horizontal = new Vector3(toTarget.x, 0f, toTarget.z);
            float d = horizontal.magnitude;
            float dy = toTarget.y;
            if (d < 1f)
                return Fail(result, $"bounce point is only {d:F2} m from the release point");
            if (release.y <= ballRadius)
                return Fail(result, "release point is at or below ground level");

            // Slowest speed that reaches the target at all (the 45-degree-ish optimum), for the error message.
            float minSpeed = Mathf.Sqrt(gravity * (dy + Mathf.Sqrt(dy * dy + d * d)));
            result.MinimumSpeedKmh = Units.MsToKmh(minSpeed);

            // Continuous projectile: tan(theta) = (s^2 - sqrt(s^4 - g(g d^2 + 2 dy s^2))) / (g d). Lower root = flat delivery.
            float s2 = speed * speed;
            float disc = s2 * s2 - gravity * (gravity * d * d + 2f * dy * s2);
            if (disc < 0f)
                return Fail(result, $"{config.speedKmh:F0} km/h cannot reach a bounce {d:F1} m away; needs at least {result.MinimumSpeedKmh:F1} km/h");

            float theta = Mathf.Atan((s2 - Mathf.Sqrt(disc)) / (gravity * d));

            // Refine for the physics engine's step-wise (semi-implicit Euler) integration, which lands slightly
            // differently from the continuous formula. Bisect the elevation until the stepped flight lands on target.
            if (fixedDeltaTime > 0f)
            {
                float lo = theta - 0.2f, hi = theta + 0.2f;
                float landLo = SimulatedRange(release, speed, lo, gravity, fixedDeltaTime, ballRadius);
                float landHi = SimulatedRange(release, speed, hi, gravity, fixedDeltaTime, ballRadius);
                if (landLo <= d && landHi >= d)
                {
                    for (int i = 0; i < MaxRefineIterations; i++)
                    {
                        float mid = 0.5f * (lo + hi);
                        float land = SimulatedRange(release, speed, mid, gravity, fixedDeltaTime, ballRadius);
                        if (land < d) lo = mid; else hi = mid;
                        theta = mid;
                        if (Mathf.Abs(land - d) < RefineTolerance) break;
                    }
                }
            }

            var dir = horizontal / d;
            result.Velocity = dir * (speed * Mathf.Cos(theta)) + Vector3.up * (speed * Mathf.Sin(theta));
            result.ElevationDegrees = theta * Mathf.Rad2Deg;
            float landed = fixedDeltaTime > 0f
                ? SimulatedRange(release, speed, theta, gravity, fixedDeltaTime, ballRadius)
                : d;
            var predicted = release + dir * landed;
            result.PredictedBounce = new Vector3(predicted.x, ballRadius, predicted.z);
            result.Reachable = true;
            return result;
        }

        /// <summary>
        /// Horizontal distance travelled before the ball centre drops to <paramref name="contactHeight"/>, integrating the
        /// way PhysX does (velocity first, then position) and interpolating within the crossing step.
        /// Returns +infinity if it never comes down within 10 s.
        /// </summary>
        public static float SimulatedRange(Vector3 release, float speed, float elevation, float gravity, float dt, float contactHeight)
        {
            float vh = speed * Mathf.Cos(elevation);
            float vy = speed * Mathf.Sin(elevation);
            float x = 0f, y = release.y;
            for (int step = 0; step < 10f / dt; step++)
            {
                vy -= gravity * dt;
                float nx = x + vh * dt;
                float ny = y + vy * dt;
                if (ny <= contactHeight)
                {
                    float f = (y - contactHeight) / (y - ny);
                    return x + (nx - x) * f;
                }
                x = nx;
                y = ny;
            }
            return float.PositiveInfinity;
        }

        static DeliverySolution Fail(DeliverySolution r, string error)
        {
            r.Reachable = false;
            r.Error = error;
            return r;
        }
    }
}
