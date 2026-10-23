using NUnit.Framework;
using UnityEngine;

namespace CricketVR.Tests
{
    public class PoseHistoryTests
    {
        const float Tol = 1e-3f;

        [Test]
        public void LinearVelocityUsesRealSampleIntervals()
        {
            var h = new PoseHistory(8);
            // Uneven frame times: velocity must still be distance / elapsed time.
            h.Add(new PoseSample(0.000, Vector3.zero, Quaternion.identity));
            h.Add(new PoseSample(0.011, new Vector3(0.022f, 0f, 0f), Quaternion.identity));
            h.Add(new PoseSample(0.030, new Vector3(0.060f, 0f, 0f), Quaternion.identity));

            Assert.IsTrue(h.TryGetVelocity(0.03, out var v, out var w));
            Assert.AreEqual(2f, v.x, Tol);
            Assert.AreEqual(0f, w.magnitude, Tol);
        }

        [Test]
        public void AngularVelocityIsRadiansPerSecondAboutTheRotationAxis()
        {
            var w = PoseHistory.AngularVelocity(Quaternion.identity, Quaternion.Euler(0f, 10f, 0f), 0.01f);
            Assert.AreEqual(10f * Mathf.Deg2Rad / 0.01f, w.y, 1e-2f);
            Assert.AreEqual(0f, w.x, Tol);
            Assert.AreEqual(0f, w.z, Tol);
        }

        [Test]
        public void AngularVelocityTakesShortestPathWhenQuaternionSignFlips()
        {
            var to = Quaternion.Euler(0f, 0f, 20f);
            var negated = new Quaternion(-to.x, -to.y, -to.z, -to.w); // same rotation, opposite sign
            var a = PoseHistory.AngularVelocity(Quaternion.identity, to, 0.02f);
            var b = PoseHistory.AngularVelocity(Quaternion.identity, negated, 0.02f);
            Assert.AreEqual(a.z, b.z, Tol);
            Assert.AreEqual(20f * Mathf.Deg2Rad / 0.02f, b.z, 1e-2f);
        }

        [Test]
        public void SmallRotationsKeepPrecision()
        {
            // 0.05 degrees in one 72 Hz frame: acos-based extraction loses this; atan2 must not.
            float dt = 1f / 72f;
            var w = PoseHistory.AngularVelocity(Quaternion.identity, Quaternion.Euler(0.05f, 0f, 0f), dt);
            Assert.AreEqual(0.05f * Mathf.Deg2Rad / dt, w.x, 0.002f);
        }

        [Test]
        public void PointVelocityAddsRotationalComponent()
        {
            // Pure rotation 10 rad/s about +Y; a point 0.8 m along +X moves at 8 m/s along -Z (Unity is left-handed: Y × X = -Z).
            var v = PoseHistory.PointVelocity(Vector3.zero, new Vector3(0f, 10f, 0f), Vector3.zero, new Vector3(0.8f, 0f, 0f));
            Assert.AreEqual(0f, v.x, Tol);
            Assert.AreEqual(-8f, v.z, Tol);
        }

        [Test]
        public void StaleOrDuplicateTimestampsAreIgnoredAndCapacityWraps()
        {
            var h = new PoseHistory(3);
            Assert.IsTrue(h.Add(new PoseSample(1.0, Vector3.zero, Quaternion.identity)));
            Assert.IsFalse(h.Add(new PoseSample(1.0, Vector3.one, Quaternion.identity)));
            Assert.IsFalse(h.Add(new PoseSample(0.5, Vector3.one, Quaternion.identity)));
            for (int i = 1; i <= 4; i++)
                h.Add(new PoseSample(1.0 + i, Vector3.right * i, Quaternion.identity));
            Assert.AreEqual(3, h.Count);
            Assert.AreEqual(4f, h.Latest.Position.x, Tol);
            Assert.AreEqual(2f, h[2].Position.x, Tol);
        }
    }

    public class BatMotionTrackerTests
    {
        static BatMotionTracker NewTracker() => new BatMotionTracker(BatMotionLimits.Default);

        static void FeedSteady(BatMotionTracker t, double start, int count, Vector3 velocity, double dt = 1.0 / 72.0)
        {
            for (int i = 0; i < count; i++)
                t.AddSample(start + i * dt, velocity * (float)(i * dt), Quaternion.identity, true);
        }

        [Test]
        public void ContactsAllowedAfterEnoughValidSamplesAndSuppressionWindow()
        {
            var t = NewTracker();
            t.Reset(MotionResetReason.Manual, 0.0);
            t.AddSample(0.01, Vector3.zero, Quaternion.identity, true);
            t.AddSample(0.02, Vector3.zero, Quaternion.identity, true);
            Assert.IsFalse(t.ContactAllowed(0.02), "only 2 samples and inside the suppression window");
            t.AddSample(0.03, Vector3.zero, Quaternion.identity, true);
            Assert.IsFalse(t.ContactAllowed(0.05), "suppression window (0.1 s) not over");
            Assert.IsTrue(t.ContactAllowed(0.11));
        }

        [Test]
        public void RecentreJumpResetsHistoryAndCannotProduceAFastSwing()
        {
            var t = NewTracker();
            FeedSteady(t, 0.0, 20, new Vector3(2f, 0f, 0f)); // gentle 2 m/s movement
            Assert.IsTrue(t.ContactAllowed(0.3));

            // Recentre: pose jumps 1.5 m in one frame (108 m/s apparent).
            double jumpTime = 20 / 72.0;
            var reason = t.AddSample(jumpTime, new Vector3(1.5f, 0f, 0f), Quaternion.identity, true);

            Assert.AreEqual(MotionResetReason.ImplausibleMotion, reason);
            Assert.AreEqual(1, t.History.Count, "history restarts from the post-jump pose");
            Assert.IsFalse(t.TryGetVelocity(out _, out _), "no velocity can span the jump");
            Assert.IsFalse(t.ContactAllowed(jumpTime + 0.05));
        }

        [Test]
        public void ExplicitOriginChangeResetsEvenWithoutAVisibleJump()
        {
            var t = NewTracker();
            FeedSteady(t, 0.0, 10, Vector3.zero);
            t.Reset(MotionResetReason.OriginChanged, 0.2);
            Assert.AreEqual(0, t.History.Count);
            Assert.AreEqual(MotionResetReason.OriginChanged, t.LastResetReason);
            Assert.IsFalse(t.ContactAllowed(0.25));
        }

        [Test]
        public void LargeRotationJumpIsTreatedAsDiscontinuity()
        {
            var t = NewTracker();
            t.AddSample(0.000, Vector3.zero, Quaternion.identity, true);
            // 170 degrees in one 72 Hz frame = ~214 rad/s: not a human swing.
            var reason = t.AddSample(1 / 72.0, Vector3.zero, Quaternion.Euler(0f, 170f, 0f), true);
            Assert.AreEqual(MotionResetReason.ImplausibleMotion, reason);
        }

        [Test]
        public void TimeGapResets()
        {
            var t = NewTracker();
            FeedSteady(t, 0.0, 5, Vector3.zero);
            Assert.AreEqual(MotionResetReason.TimeGap, t.AddSample(1.0, Vector3.zero, Quaternion.identity, true));
        }

        [Test]
        public void LosingTrackingResetsOnceAndRecoveryNeedsFreshSamples()
        {
            var t = NewTracker();
            FeedSteady(t, 0.0, 10, Vector3.zero);
            double lost = 10 / 72.0;
            Assert.AreEqual(MotionResetReason.TrackingLost, t.AddSample(lost, Vector3.zero, Quaternion.identity, false));
            Assert.AreEqual(MotionResetReason.None, t.AddSample(lost + 0.01, Vector3.zero, Quaternion.identity, false));
            int resets = t.ResetCount;

            // Tracking returns 0.05 s later: no further reset, but contacts wait for samples and the window.
            t.AddSample(lost + 0.05, Vector3.one, Quaternion.identity, true);
            Assert.AreEqual(resets, t.ResetCount);
            Assert.IsFalse(t.ContactAllowed(lost + 0.06));
        }

        [Test]
        public void FastButPlausibleSwingIsAccepted()
        {
            var t = NewTracker();
            // Hands at 12 m/s, rotating 25 rad/s: a hard drive. Must not be mistaken for a tracking jump.
            double dt = 1 / 72.0;
            for (int i = 0; i < 10; i++)
            {
                var r = t.AddSample(i * dt, new Vector3(12f * (float)(i * dt), 0f, 0f),
                    Quaternion.Euler(0f, 25f * Mathf.Rad2Deg * (float)(i * dt), 0f), true);
                Assert.AreEqual(MotionResetReason.None, r, $"sample {i}");
            }
            Assert.IsTrue(t.TryGetVelocity(out var v, out var w));
            Assert.AreEqual(12f, v.x, 0.01f);
            Assert.AreEqual(25f, w.y, 0.05f);
        }

        [Test]
        public void NonFiniteSampleIsRejected()
        {
            var t = NewTracker();
            t.AddSample(0.0, Vector3.zero, Quaternion.identity, true);
            Assert.AreEqual(MotionResetReason.ImplausibleMotion,
                t.AddSample(0.01, new Vector3(float.NaN, 0f, 0f), Quaternion.identity, true));
        }
    }

    public class BatFrameTests
    {
        [Test]
        public void DefaultBatRunsAlongHandleWithBladePastTheRingAndFaceParallelToButtons()
        {
            foreach (var hand in new[] { BatHand.Right, BatHand.Left })
            {
                var r = BatFrame.BaseRotation(hand);
                // Toe direction (bat -Y) = grip +Z (past the ring); face normal (bat +Z) = grip +Y (button face).
                Assert.That(Vector3.Angle(r * Vector3.down, Vector3.forward), Is.LessThan(0.01f), hand.ToString());
                Assert.That(Vector3.Angle(r * Vector3.forward, Vector3.up), Is.LessThan(0.01f), hand.ToString());
            }
        }

        [Test]
        public void OffsetsRotateAboutTheirOwnBatAxes()
        {
            var b = BatFrame.BaseRotation(BatHand.Right);
            // Face turn spins about the handle: the handle axis is unchanged.
            var turned = BatFrame.GripRotation(BatHand.Right, 0f, 90f, 0f);
            Assert.That(Vector3.Angle(turned * Vector3.up, b * Vector3.up), Is.LessThan(0.01f));
            // Lean rotates about the face normal: the face normal is unchanged.
            var leaned = BatFrame.GripRotation(BatHand.Right, 0f, 0f, 30f);
            Assert.That(Vector3.Angle(leaned * Vector3.forward, b * Vector3.forward), Is.LessThan(0.01f));
            Assert.That(Vector3.Angle(leaned * Vector3.up, b * Vector3.up), Is.EqualTo(30f).Within(0.01f));
            // Tilt rotates about the blade width: the width axis is unchanged.
            var tilted = BatFrame.GripRotation(BatHand.Right, 20f, 0f, 0f);
            Assert.That(Vector3.Angle(tilted * Vector3.right, b * Vector3.right), Is.LessThan(0.01f));
        }

        [Test]
        public void FaceFlipSwapsFaceAndBackButKeepsTheBatAngle()
        {
            var normal = BatFrame.GripRotation(BatHand.Right, 35f, 5f, 0f, faceFlipped: false);
            var flipped = BatFrame.GripRotation(BatHand.Right, 35f, 5f, 0f, faceFlipped: true);
            Assert.That(Vector3.Angle(flipped * Vector3.up, normal * Vector3.up), Is.LessThan(0.01f), "long axis unchanged");
            Assert.That(Vector3.Angle(flipped * Vector3.forward, -(normal * Vector3.forward)), Is.LessThan(0.01f), "face reversed");
        }

        [Test]
        public void SpinTwistsAboutTheTiltedHandleWithoutChangingTheBatAngle()
        {
            var baseline = BatFrame.GripRotation(BatHand.Right, 35f, 5f, 0f, true, 0f);
            var spun = BatFrame.GripRotation(BatHand.Right, 35f, 5f, 0f, true, 40f);
            Assert.That(Vector3.Angle(spun * Vector3.up, baseline * Vector3.up), Is.LessThan(0.01f), "handle direction unchanged");
            Assert.That(Vector3.Angle(spun * Vector3.forward, baseline * Vector3.forward), Is.EqualTo(40f).Within(0.01f), "face twisted 40 degrees");
        }

        [Test]
        public void BladeHangsBelowGrip()
        {
            var g = BatGeometry.Default;
            Assert.Less(g.Toe.y, g.BladeCenter.y);
            Assert.AreEqual(-(g.gripToBlade + g.bladeLength), g.Toe.y, 1e-5f);
        }
    }
}
