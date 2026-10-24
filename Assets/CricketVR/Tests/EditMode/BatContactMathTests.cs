using NUnit.Framework;
using UnityEngine;

namespace CricketVR.Tests
{
    /// <summary>
    /// Bat frame = world here: face +Z (toward the bowler), handle +Y, toe -Y. The ball comes in along -Z.
    /// </summary>
    public class BatContactMathTests
    {
        static readonly BatGeometry G = BatGeometry.Default;
        static readonly BatResponseTuning T = BatResponseTuning.Default;
        const float R = 0.036f;
        static Vector3 Half => G.BladeSize * 0.5f;
        static Pose Still => new Pose(Vector3.zero, Quaternion.identity);
        static Vector3 SweetLocal => new Vector3(0f, G.Toe.y + T.sweetSpotFromToe, G.bladeThickness * 0.5f);

        static BatResponse Respond(Vector3 ballVelocity, Vector3 batVelocity, Vector3 normal, Vector3 localPoint) =>
            BatContactMath.Resolve(ballVelocity, batVelocity, normal, localPoint, Quaternion.identity, G, T);

        // ---------- Sweep ----------

        [Test]
        public void FastBallCrossingTheBladeInOneStepIsCaught()
        {
            // 0.6 m in a single step: far more than blade thickness + ball diameter.
            var y = SweetLocal.y;
            var hit = BatContactMath.Sweep(Still, Still, G.BladeCenter, Half, new Vector3(0f, y, 0.3f), new Vector3(0f, y, -0.3f), R);
            Assert.IsTrue(hit.Hit);
            Assert.IsFalse(hit.InitialOverlap);
            Assert.That(Vector3.Angle(hit.Normal, Vector3.forward), Is.LessThan(1f), "face normal toward the incoming ball");
            Assert.AreEqual(G.bladeThickness * 0.5f + R, hit.BallCentre.z, 0.002f, "ball centre one radius in front of the face");
        }

        [Test]
        public void RotatingBatSweepingThroughAStationaryBallMidStepIsCaught()
        {
            // The bat swings 120 degrees about the grip in one step. The ball sits where the blade passes halfway
            // through: neither the start nor the end pose touches it (a pose-only check would miss it).
            var start = new Pose(Vector3.zero, Quaternion.Euler(-60f, 0f, 0f));
            var end = new Pose(Vector3.zero, Quaternion.Euler(60f, 0f, 0f));
            var ball = new Vector3(0f, -0.5f, 0f); // on the arc, at the bottom of the swing
            Assert.IsFalse(BatContactMath.Sweep(start, start, G.BladeCenter, Half, ball, ball, R).Hit, "start pose alone misses");
            Assert.IsFalse(BatContactMath.Sweep(end, end, G.BladeCenter, Half, ball, ball, R).Hit, "end pose alone misses");

            var hit = BatContactMath.Sweep(start, end, G.BladeCenter, Half, ball, ball, R);
            Assert.IsTrue(hit.Hit);
            Assert.That(hit.Fraction, Is.InRange(0.3f, 0.6f));
            Assert.Greater(hit.Substeps, 10, "substeps sized to the toe's travel");
        }

        [Test]
        public void BallPassingBesideTheBladeIsNotAHit()
        {
            float y = SweetLocal.y;
            float x = G.bladeWidth * 0.5f + R + 0.01f;
            Assert.IsFalse(BatContactMath.Sweep(Still, Still, G.BladeCenter, Half, new Vector3(x, y, 0.5f), new Vector3(x, y, -0.5f), R).Hit);
        }

        [Test]
        public void InitialOverlapIsReportedAndPushesOutThroughTheNearestFace()
        {
            var inside = new Vector3(0.01f, SweetLocal.y, 0.015f); // centre inside, nearest to the face
            var hit = BatContactMath.Sweep(Still, Still, G.BladeCenter, Half, inside, inside + Vector3.back * 0.1f, R);
            Assert.IsTrue(hit.Hit);
            Assert.IsTrue(hit.InitialOverlap);
            Assert.AreEqual(0f, hit.Fraction);
            Assert.That(Vector3.Angle(hit.Normal, Vector3.forward), Is.LessThan(1f));
        }

        // ---------- Response ----------

        [Test]
        public void StationaryFaceDeflectsTheBallBackWithRestitution()
        {
            var r = Respond(new Vector3(0f, 0f, -22f), Vector3.zero, Vector3.forward, SweetLocal);
            Assert.IsTrue(r.Applied);
            Assert.AreEqual(1f, r.SweetFactor, 1e-3f);
            Assert.AreEqual(T.restitutionSweet * 22f, r.ExitVelocity.z, 0.01f);
            Assert.AreEqual(0f, r.ExitVelocity.x, 1e-4f);
        }

        [Test]
        public void SoftDefenceSlowerThanBlockSlowerThanDrive()
        {
            var ball = new Vector3(0f, 0f, -22f);
            float soft = Respond(ball, new Vector3(0f, 0f, -3f), Vector3.forward, SweetLocal).ExitVelocity.magnitude;
            float block = Respond(ball, Vector3.zero, Vector3.forward, SweetLocal).ExitVelocity.magnitude;
            float drive = Respond(ball, new Vector3(0f, 0f, 15f), Vector3.forward, SweetLocal).ExitVelocity.magnitude;
            Assert.Less(soft, block);
            Assert.Less(block, drive);
            Assert.Greater(drive, 25f, "a 15 m/s swing should send the ball well over 90 km/h");
        }

        [Test]
        public void FaceAngleSetsDirectionAndLoftSetsLaunchAngle()
        {
            var ball = new Vector3(0f, 0f, -22f);
            var bat = new Vector3(0f, 0f, 10f);
            // Face turned 20 degrees toward +X (off side): ball goes toward +X.
            var turned = Quaternion.Euler(0f, 20f, 0f) * Vector3.forward;
            var dir = Respond(ball, bat, turned, SweetLocal).ExitVelocity;
            Assert.Greater(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 10f);
            // Face opened upward 20 degrees: ball goes up.
            var lofted = Quaternion.Euler(-20f, 0f, 0f) * Vector3.forward;
            var up = Respond(ball, bat, lofted, SweetLocal).ExitVelocity;
            Assert.Greater(Mathf.Atan2(up.y, new Vector2(up.x, up.z).magnitude) * Mathf.Rad2Deg, 10f);
        }

        [Test]
        public void OffCentreIsLessCleanThanTheSweetSpot()
        {
            var ball = new Vector3(0f, 0f, -22f);
            var bat = new Vector3(0f, 0f, 12f);
            var toe = new Vector3(0f, G.Toe.y + 0.02f, SweetLocal.z);
            var sweet = Respond(ball, bat, Vector3.forward, SweetLocal);
            var offToe = Respond(ball, bat, Vector3.forward, toe);
            Assert.Less(offToe.SweetFactor, 0.6f);
            Assert.Less(offToe.ExitVelocity.magnitude, sweet.ExitVelocity.magnitude);
        }

        [Test]
        public void EdgeContactDeflectsTowardThatEdgeAndLosesPace()
        {
            var ball = new Vector3(0f, 0f, -22f);
            var middle = Respond(ball, Vector3.zero, Vector3.forward, SweetLocal);
            var edgePoint = new Vector3(G.bladeWidth * 0.5f, SweetLocal.y, SweetLocal.z);
            var edge = Respond(ball, Vector3.zero, Vector3.forward, edgePoint);
            Assert.AreEqual(1f, edge.EdgeFactor, 1e-3f);
            Assert.Greater(edge.ExitVelocity.x, 0.5f, "deflects toward the +X edge");
            Assert.Less(edge.ExitVelocity.magnitude, middle.ExitVelocity.magnitude);
        }

        [Test]
        public void SwingAssistSpeedsUpDrivesButLeavesAStationaryBlockAlone()
        {
            var ball = new Vector3(0f, 0f, -22f);
            var bat = new Vector3(0f, 0f, 10f);
            var noAssist = T;
            noAssist.batSpeedGain = 1f;
            float assisted = Respond(ball, bat, Vector3.forward, SweetLocal).ExitVelocity.magnitude;
            float plain = BatContactMath.Resolve(ball, bat, Vector3.forward, SweetLocal, Quaternion.identity, G, noAssist).ExitVelocity.magnitude;
            Assert.Greater(assisted, plain + 4f);
            Assert.AreEqual(Respond(ball, Vector3.zero, Vector3.forward, SweetLocal).ExitVelocity.magnitude,
                BatContactMath.Resolve(ball, Vector3.zero, Vector3.forward, SweetLocal, Quaternion.identity, G, noAssist).ExitVelocity.magnitude, 1e-4f);
        }

        [Test]
        public void SeparatingContactIsIgnored()
        {
            // Bat moving away from the ball faster than the ball approaches.
            var r = Respond(new Vector3(0f, 0f, -5f), new Vector3(0f, 0f, -8f), Vector3.forward, SweetLocal);
            Assert.IsFalse(r.Applied);
        }

        [Test]
        public void RandomContactsAlwaysGiveFiniteBoundedVelocities()
        {
            var rng = new System.Random(5);
            float F(float a) => (float)(rng.NextDouble() * 2 - 1) * a;
            for (int i = 0; i < 5000; i++)
            {
                var normal = new Vector3(F(1), F(1), F(1));
                if (normal.sqrMagnitude < 1e-4f) continue;
                normal.Normalize();
                var local = new Vector3(F(G.bladeWidth * 0.5f), G.BladeCenter.y + F(G.bladeLength * 0.5f), F(0.02f));
                var r = BatContactMath.Resolve(new Vector3(F(60), F(60), F(60)), new Vector3(F(50), F(50), F(50)),
                    normal, local, Quaternion.Euler(F(180), F(180), F(180)), G, T);
                if (!r.Applied) continue;
                var v = r.ExitVelocity;
                Assert.IsTrue(float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z), $"case {i}");
                Assert.LessOrEqual(v.magnitude, T.maxExitSpeed + 1e-3f, $"case {i}");
            }
        }
    }
}
