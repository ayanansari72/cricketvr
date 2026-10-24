using System;
using UnityEngine;

namespace CricketVR
{
    /// <summary>Result of a swept bat/ball test over one physics step.</summary>
    public struct SweptContact
    {
        public bool Hit;
        /// <summary>Fraction of the step (0..1) at which the ball first touches the blade.</summary>
        public float Fraction;
        /// <summary>True if the ball already touched the blade at the start of the step.</summary>
        public bool InitialOverlap;
        /// <summary>Closest point on the blade surface (world).</summary>
        public Vector3 Point;
        /// <summary>Unit normal from the blade surface toward the ball centre (world).</summary>
        public Vector3 Normal;
        /// <summary>Ball centre at the moment of contact (world).</summary>
        public Vector3 BallCentre;
        /// <summary>Contact point in the bat frame (+Y handle, -Y toe, +Z face, X width).</summary>
        public Vector3 LocalPoint;
        public Pose BatPose;
        public int Substeps;
    }

    [Serializable]
    public struct BatResponseTuning
    {
        [Tooltip("Bat-ball restitution at the sweet spot (relative normal speed kept).")]
        public float restitutionSweet;
        [Tooltip("Restitution far from the sweet spot (toe, shoulder).")]
        public float restitutionOffCentre;
        [Tooltip("Sweet spot distance up from the toe (m).")]
        public float sweetSpotFromToe;
        [Tooltip("How quickly the response falls off away from the sweet spot along the blade (m).")]
        public float sweetSpotFalloff;
        [Tooltip("Outer fraction of the blade's half-width that counts as an edge (0..1).")]
        public float edgeZone;
        [Tooltip("Restitution multiplier for a full edge contact.")]
        public float edgeRestitutionScale;
        [Tooltip("Maximum deflection of the normal toward the edge for a full edge contact (degrees).")]
        public float edgeDeflectDegrees;
        [Tooltip("Fraction of the tangential relative velocity removed by the contact (0 = frictionless, 1 = full grip).")]
        public float tangentialLoss;
        [Tooltip("Exit speed cap (m/s).")]
        public float maxExitSpeed;
        [Tooltip("Multiplier on the measured bat speed at the contact point. A game assist: a weightless controller held " +
                 "near the blade is swung slower than a real 1.2 kg bat (owner: hard swings 'not powerful enough', E-028). " +
                 "1 = no assist.")]
        public float batSpeedGain;

        public static BatResponseTuning Default => new BatResponseTuning
        {
            restitutionSweet = 0.5f,
            restitutionOffCentre = 0.35f,
            sweetSpotFromToe = 0.20f,
            sweetSpotFalloff = 0.22f,
            edgeZone = 0.25f,
            edgeRestitutionScale = 0.55f,
            edgeDeflectDegrees = 14f,
            tangentialLoss = 0.25f,
            maxExitSpeed = 45f,
            batSpeedGain = 1.4f,
        };
    }

    public struct BatResponse
    {
        public bool Applied;
        public Vector3 ExitVelocity;
        /// <summary>Relative normal approach speed (m/s, positive).</summary>
        public float ApproachSpeed;
        public float Restitution;
        /// <summary>1 at the sweet spot, falling toward 0 away from it.</summary>
        public float SweetFactor;
        /// <summary>-1..1: how far into the left/right edge zone (0 = middle of the blade).</summary>
        public float EdgeFactor;
        public string Note;
    }

    public static class BatContactMath
    {
        const int MaxSubsteps = 64;
        const int RefineIterations = 12;

        /// <summary>
        /// Swept test of a moving sphere against a moving, rotating box (the blade) over one physics step.
        /// Both bodies are interpolated together (bat: lerp/slerp; ball: straight segment), so a bat rotating
        /// through the ball between samples is still caught. Substeps are sized so neither the ball nor any blade
        /// corner moves more than half the ball radius per substep; the first touching substep is then refined
        /// by bisection.
        /// </summary>
        /// <param name="bladeCentre">Blade box centre in the bat frame.</param>
        /// <param name="bladeHalfSize">Blade box half extents in the bat frame.</param>
        public static SweptContact Sweep(Pose batStart, Pose batEnd, Vector3 bladeCentre, Vector3 bladeHalfSize,
            Vector3 ballStart, Vector3 ballEnd, float radius)
        {
            var result = new SweptContact();

            float maxMove = (ballEnd - ballStart).magnitude;
            // Largest blade-corner displacement covers translation and rotation together.
            for (int c = 0; c < 8; c++)
            {
                var corner = bladeCentre + Vector3.Scale(bladeHalfSize, new Vector3((c & 1) * 2 - 1, (c & 2) - 1, ((c & 4) >> 1) - 1));
                var a = batStart.position + batStart.rotation * corner;
                var b = batEnd.position + batEnd.rotation * corner;
                maxMove = Mathf.Max(maxMove, (b - a).magnitude);
            }
            int n = Mathf.Clamp(Mathf.CeilToInt(maxMove / (0.5f * radius)), 1, MaxSubsteps);
            result.Substeps = n;

            if (Touching(0f, out var first))
            {
                first.InitialOverlap = true;
                first.Substeps = n;
                return first;
            }

            float prev = 0f;
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n;
                if (!Touching(t, out _))
                {
                    prev = t;
                    continue;
                }
                // Bisect between the last clear and first touching fraction for the moment of contact.
                float lo = prev, hi = t;
                for (int k = 0; k < RefineIterations; k++)
                {
                    float mid = 0.5f * (lo + hi);
                    if (Touching(mid, out _)) hi = mid; else lo = mid;
                }
                Touching(hi, out var hit);
                hit.Substeps = n;
                return hit;
            }
            return result;

            bool Touching(float t, out SweptContact contact)
            {
                var pose = new Pose(Vector3.Lerp(batStart.position, batEnd.position, t),
                    Quaternion.Slerp(batStart.rotation, batEnd.rotation, t));
                var centre = Vector3.Lerp(ballStart, ballEnd, t);
                contact = default;

                var local = Quaternion.Inverse(pose.rotation) * (centre - pose.position) - bladeCentre;
                var closest = new Vector3(
                    Mathf.Clamp(local.x, -bladeHalfSize.x, bladeHalfSize.x),
                    Mathf.Clamp(local.y, -bladeHalfSize.y, bladeHalfSize.y),
                    Mathf.Clamp(local.z, -bladeHalfSize.z, bladeHalfSize.z));
                var offset = local - closest;
                float dist = offset.magnitude;
                if (dist > radius)
                    return false;

                Vector3 localNormal;
                if (dist > 1e-6f)
                {
                    localNormal = offset / dist;
                }
                else
                {
                    // Centre inside the blade: push out through the nearest face.
                    var gap = bladeHalfSize - new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
                    if (gap.z <= gap.x && gap.z <= gap.y) localNormal = new Vector3(0f, 0f, Mathf.Sign(local.z));
                    else if (gap.x <= gap.y) localNormal = new Vector3(Mathf.Sign(local.x), 0f, 0f);
                    else localNormal = new Vector3(0f, Mathf.Sign(local.y), 0f);
                    if (localNormal.z == 0f && localNormal.x == 0f && localNormal.y == 0f) localNormal = Vector3.forward;
                }

                var batLocalPoint = closest + bladeCentre;
                contact.Hit = true;
                contact.Fraction = t;
                contact.BallCentre = centre;
                contact.LocalPoint = batLocalPoint;
                contact.Point = pose.position + pose.rotation * batLocalPoint;
                contact.Normal = (pose.rotation * localNormal).normalized;
                contact.BatPose = pose;
                return true;
            }
        }

        /// <summary>
        /// Outgoing ball velocity for a bat contact. The bat is treated as much heavier than the ball (it is
        /// held), so only the ball's velocity changes:
        ///   v_rel = v_ball - v_bat;  v_out_rel = -e (v_rel·n) n' + (1 - loss) v_rel_t;  v_out = v_bat + v_out_rel
        /// where v_bat is the bat's velocity AT THE CONTACT POINT (including rotation), e falls off away from the
        /// sweet spot and toward the edges, and n' tilts toward an edge for edge contacts. Deterministic and bounded.
        /// </summary>
        /// <param name="localPoint">Contact point in the bat frame.</param>
        /// <param name="batRotation">Bat orientation at contact (to express the edge axis in world space).</param>
        public static BatResponse Resolve(Vector3 ballVelocity, Vector3 batPointVelocity, Vector3 normal,
            Vector3 localPoint, Quaternion batRotation, BatGeometry geometry, BatResponseTuning tuning)
        {
            var r = new BatResponse();
            batPointVelocity *= tuning.batSpeedGain > 0f ? tuning.batSpeedGain : 1f;
            var vRel = ballVelocity - batPointVelocity;
            float vn = Vector3.Dot(vRel, normal);
            if (!(vn < 0f))
            {
                r.Note = "separating";
                return r;
            }
            r.ApproachSpeed = -vn;

            // Sweet spot: along the blade from the toe.
            float fromToe = localPoint.y - geometry.Toe.y;
            float k = (fromToe - tuning.sweetSpotFromToe) / Mathf.Max(1e-3f, tuning.sweetSpotFalloff);
            r.SweetFactor = Mathf.Exp(-k * k);

            // Edge: outer part of the blade's width.
            float across = localPoint.x / (geometry.bladeWidth * 0.5f);
            float edgeStart = 1f - Mathf.Clamp01(tuning.edgeZone);
            float edge = Mathf.Clamp01((Mathf.Abs(across) - edgeStart) / Mathf.Max(1e-3f, 1f - edgeStart));
            r.EdgeFactor = edge * Mathf.Sign(across);

            float e = Mathf.Lerp(tuning.restitutionOffCentre, tuning.restitutionSweet, r.SweetFactor);
            e *= Mathf.Lerp(1f, tuning.edgeRestitutionScale, edge);
            r.Restitution = e;

            // Edge contacts deflect toward the edge they came off (a nick), by a bounded angle.
            var n = normal;
            if (edge > 0f)
            {
                var edgeAxis = batRotation * new Vector3(Mathf.Sign(across), 0f, 0f);
                var axis = Vector3.Cross(n, edgeAxis);
                if (axis.sqrMagnitude > 1e-8f)
                    n = Quaternion.AngleAxis(tuning.edgeDeflectDegrees * edge, axis.normalized) * n;
            }

            var vTangent = vRel - vn * normal;
            var vOutRel = -e * vn * n + (1f - Mathf.Clamp01(tuning.tangentialLoss)) * vTangent;
            var vOut = batPointVelocity + vOutRel;

            if (vOut.magnitude > tuning.maxExitSpeed)
                vOut = vOut.normalized * tuning.maxExitSpeed;

            if (!IsFinite(vOut))
            {
                // Never launch a ball with NaN/infinite velocity: drop it dead off the bat instead.
                vOut = new Vector3(0f, -1f, 0f);
                r.Note = "non-finite result replaced with a dead-bat drop";
            }
            r.ExitVelocity = vOut;
            r.Applied = true;
            return r;
        }

        static bool IsFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
