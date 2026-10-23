using System;
using UnityEngine;

namespace CricketVR
{
    public enum MotionResetReason
    {
        None,
        TrackingLost,
        OriginChanged,
        TimeGap,
        ImplausibleMotion,
        FocusChanged,
        HandChanged,
        Manual,
    }

    [Serializable]
    public struct BatMotionLimits
    {
        [Tooltip("A gap longer than this between samples (s) discards history.")]
        public float maxSampleGap;
        [Tooltip("Grip speed above this (m/s) is treated as a tracking jump, not a swing.")]
        public float maxGripSpeed;
        [Tooltip("Angular speed above this (rad/s) is treated as a tracking jump.")]
        public float maxAngularSpeed;
        [Tooltip("Contacts are suppressed for this long (s) after any reset.")]
        public float suppressAfterReset;
        [Tooltip("Valid samples needed after a reset before contacts are allowed.")]
        public int minValidSamples;
        [Tooltip("Velocity estimation window (s).")]
        public float velocityWindow;

        public static BatMotionLimits Default => new BatMotionLimits
        {
            maxSampleGap = 0.1f,
            maxGripSpeed = 25f,
            maxAngularSpeed = 60f,
            suppressAfterReset = 0.1f,
            minValidSamples = 3,
            velocityWindow = 0.03f,
        };
    }

    /// <summary>
    /// Decides whether bat motion can be trusted. A tracking discontinuity (lost tracking, recentre,
    /// time gap, impossible jump) clears the history and suppresses contacts for a short time,
    /// so a pose jump can never be read as a fast swing (REQ-007, AC-03).
    /// </summary>
    public sealed class BatMotionTracker
    {
        readonly PoseHistory _history;
        double _suppressedUntil = double.NegativeInfinity;
        int _validSinceReset;

        public BatMotionTracker(BatMotionLimits limits, int capacity = 16)
        {
            Limits = limits;
            _history = new PoseHistory(capacity);
        }

        public BatMotionLimits Limits { get; set; }
        public PoseHistory History => _history;
        public MotionResetReason LastResetReason { get; private set; }
        public double LastResetTime { get; private set; } = double.NegativeInfinity;
        public int ResetCount { get; private set; }

        /// <summary>True when the history is long and recent enough for contact calculations.</summary>
        public bool ContactAllowed(double now) =>
            now >= _suppressedUntil && _validSinceReset >= Limits.minValidSamples && _history.Count >= 2;

        /// <summary>Clears history and starts the suppression window.</summary>
        public void Reset(MotionResetReason reason, double now)
        {
            _history.Clear();
            _validSinceReset = 0;
            _suppressedUntil = now + Limits.suppressAfterReset;
            LastResetReason = reason;
            LastResetTime = now;
            ResetCount++;
        }

        /// <summary>
        /// Feeds one tracked pose. Returns the reset reason if this sample revealed a discontinuity
        /// (the history is then restarted from this sample), otherwise <see cref="MotionResetReason.None"/>.
        /// </summary>
        public MotionResetReason AddSample(double time, Vector3 position, Quaternion rotation, bool isTracked)
        {
            if (!isTracked)
            {
                // Reset once on the transition; stay empty while untracked.
                if (_history.Count > 0 || LastResetReason != MotionResetReason.TrackingLost)
                {
                    Reset(MotionResetReason.TrackingLost, time);
                    return MotionResetReason.TrackingLost;
                }
                return MotionResetReason.None;
            }

            if (!IsFinite(position) || !IsFinite(rotation))
            {
                Reset(MotionResetReason.ImplausibleMotion, time);
                return MotionResetReason.ImplausibleMotion;
            }

            var reason = MotionResetReason.None;
            if (_history.Count > 0)
            {
                var last = _history.Latest;
                double dt = time - last.Time;
                if (dt <= 0)
                    return MotionResetReason.None; // duplicate or stale timestamp: ignore

                if (dt > Limits.maxSampleGap)
                    reason = MotionResetReason.TimeGap;
                else
                {
                    float speed = (position - last.Position).magnitude / (float)dt;
                    float omega = PoseHistory.AngularVelocity(last.Rotation, rotation, (float)dt).magnitude;
                    if (speed > Limits.maxGripSpeed || omega > Limits.maxAngularSpeed)
                        reason = MotionResetReason.ImplausibleMotion;
                }
            }

            if (reason != MotionResetReason.None)
                Reset(reason, time);

            _history.Add(new PoseSample(time, position, rotation));
            _validSinceReset++;
            return reason;
        }

        public bool TryGetVelocity(out Vector3 linear, out Vector3 angular) =>
            _history.TryGetVelocity(Limits.velocityWindow, out linear, out angular);

        static bool IsFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        static bool IsFinite(Quaternion q) => float.IsFinite(q.x) && float.IsFinite(q.y) && float.IsFinite(q.z) && float.IsFinite(q.w);
    }
}
