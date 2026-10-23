using NUnit.Framework;
using UnityEngine;

namespace CricketVR.Tests
{
    public class DeliverySolverTests
    {
        const float G = 9.81f, Dt = 0.02f, R = 0.036f;

        static DeliverySolution Solve(float kmh, float line, float length, float dt = Dt) =>
            DeliverySolver.Solve(DeliveryConfig.Create("t", kmh, line, length), G, dt, R);

        [Test]
        public void KmhToMsDividesBy3Point6()
        {
            Assert.AreEqual(22.2222f, Units.KmhToMs(80f), 1e-3f);
            Assert.AreEqual(80f, Units.MsToKmh(Units.KmhToMs(80f)), 1e-3f);
        }

        [Test]
        public void ReleaseSpeedIsExactlyTheRequestedSpeed()
        {
            foreach (var kmh in new[] { 60f, 80f, 100f })
            {
                var s = Solve(kmh, 0f, 6f);
                Assert.IsTrue(s.Reachable, s.Error);
                Assert.AreEqual(Units.KmhToMs(kmh), s.Velocity.magnitude, 1e-3f, $"{kmh} km/h");
                Assert.Less(s.Velocity.z, 0f, "deliveries travel toward -Z");
            }
        }

        [Test]
        public void ContinuousSolutionLandsOnTarget()
        {
            var s = Solve(80f, 0f, 6f, dt: 0f);
            // Integrate the exact projectile to y = R.
            var p0 = FieldDimensions.DefaultReleasePoint;
            float vy = s.Velocity.y, a = -0.5f * G, b = vy, c = p0.y - R;
            float t = (-b - Mathf.Sqrt(b * b - 4f * a * c)) / (2f * a);
            var land = p0 + s.Velocity * t;
            Assert.AreEqual(6f, land.z, 0.005f);
            Assert.AreEqual(0f, land.x, 0.005f);
        }

        [Test]
        public void SteppedPhysicsLandsWithinOneCentimetre()
        {
            foreach (var length in new[] { 3.5f, 6f, 9f })
            {
                var s = Solve(80f, 0f, length);
                Assert.IsTrue(s.Reachable, s.Error);
                Assert.AreEqual(length, s.PredictedBounce.z, 0.01f, $"length {length}");
            }
        }

        [Test]
        public void LengthChangesTheTrajectory()
        {
            // A fuller length (bounce nearer the batter) needs a higher release angle at the same speed.
            float full = Solve(80f, 0f, 3.5f).ElevationDegrees;
            float good = Solve(80f, 0f, 6f).ElevationDegrees;
            float shortBall = Solve(80f, 0f, 9f).ElevationDegrees;
            Assert.Greater(full, good);
            Assert.Greater(good, shortBall);
        }

        [Test]
        public void LineMovesTheBounceSideways()
        {
            var s = Solve(80f, 0.25f, 6f);
            Assert.Greater(s.Velocity.x, 0f);
            Assert.AreEqual(0.25f, s.PredictedBounce.x, 0.01f);
        }

        [Test]
        public void UnreachableRequestIsReportedNotAdjusted()
        {
            // From a 2 m release, a 6 m length is 12.9 m away: the slowest possible (lofted) delivery is ~37.5 km/h.
            var s = Solve(30f, 0f, 6f);
            Assert.IsFalse(s.Reachable);
            Assert.AreEqual(Vector3.zero, s.Velocity, "no velocity for a rejected request");
            StringAssert.Contains("30 km/h", s.Error);
            Assert.That(s.MinimumSpeedKmh, Is.InRange(36f, 39f));
            // Just above the reported minimum, it becomes reachable.
            Assert.IsTrue(Solve(s.MinimumSpeedKmh + 1f, 0f, 6f).Reachable);
        }

        [Test]
        public void InvalidInputsAreRejected()
        {
            Assert.IsFalse(Solve(0f, 0f, 6f).Reachable);
            Assert.IsFalse(Solve(float.NaN, 0f, 6f).Reachable);
            Assert.IsFalse(Solve(80f, 0f, FieldDimensions.DefaultReleasePoint.z).Reachable, "bounce under the release point");
        }
    }
}
