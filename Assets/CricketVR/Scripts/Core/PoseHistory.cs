using UnityEngine;

namespace CricketVR
{
    public struct PoseSample
    {
        public double Time;
        public Vector3 Position;
        public Quaternion Rotation;

        public PoseSample(double time, Vector3 position, Quaternion rotation)
        {
            Time = time;
            Position = position;
            Rotation = rotation;
        }
    }

    /// <summary>
    /// Fixed-capacity ring buffer of timestamped world poses with velocity estimates.
    /// Velocities use the real time between samples, never an assumed frame rate,
    /// because render and physics ticks don't line up. No allocations after construction.
    /// </summary>
    public sealed class PoseHistory
    {
        readonly PoseSample[] _samples;
        int _newest = -1;

        public PoseHistory(int capacity = 16)
        {
            _samples = new PoseSample[Mathf.Max(2, capacity)];
        }

        public int Count { get; private set; }
        public int Capacity => _samples.Length;

        public void Clear()
        {
            Count = 0;
            _newest = -1;
        }

        /// <summary>Adds a sample. Samples not strictly newer than the latest are ignored.</summary>
        public bool Add(PoseSample sample)
        {
            if (Count > 0 && sample.Time <= Latest.Time)
                return false;
            _newest = (_newest + 1) % _samples.Length;
            _samples[_newest] = sample;
            if (Count < _samples.Length)
                Count++;
            return true;
        }

        public PoseSample Latest => this[0];

        /// <summary>0 = newest, 1 = the one before, ...</summary>
        public PoseSample this[int ago]
        {
            get
            {
                if (ago < 0 || ago >= Count)
                    throw new System.ArgumentOutOfRangeException(nameof(ago));
                int i = (_newest - ago + _samples.Length) % _samples.Length;
                return _samples[i];
            }
        }

        /// <summary>
        /// Velocity between the newest sample and the newest sample at least <paramref name="window"/> seconds older
        /// (falling back to the oldest stored). Angular velocity is in radians per second, world space.
        /// </summary>
        public bool TryGetVelocity(double window, out Vector3 linear, out Vector3 angular)
        {
            linear = Vector3.zero;
            angular = Vector3.zero;
            if (Count < 2)
                return false;

            var newest = this[0];
            var older = this[1];
            for (int ago = 1; ago < Count; ago++)
            {
                older = this[ago];
                if (newest.Time - older.Time >= window)
                    break;
            }

            float dt = (float)(newest.Time - older.Time);
            if (dt <= 0f)
                return false;

            linear = (newest.Position - older.Position) / dt;
            angular = AngularVelocity(older.Rotation, newest.Rotation, dt);
            return true;
        }

        /// <summary>
        /// World-space angular velocity (rad/s) taking <paramref name="from"/> to <paramref name="to"/> in <paramref name="dt"/>,
        /// along the shortest path. Uses atan2 rather than acos so small rotations stay precise.
        /// </summary>
        public static Vector3 AngularVelocity(Quaternion from, Quaternion to, float dt)
        {
            var delta = to * Quaternion.Inverse(from);
            // q and -q are the same rotation; pick the one with w >= 0 so the angle is <= 180 degrees.
            if (delta.w < 0f)
                delta = new Quaternion(-delta.x, -delta.y, -delta.z, -delta.w);

            var v = new Vector3(delta.x, delta.y, delta.z);
            float sinHalf = v.magnitude;
            if (sinHalf < 1e-7f || dt <= 0f)
                return Vector3.zero;

            float angle = 2f * Mathf.Atan2(sinHalf, delta.w);
            return v / sinHalf * (angle / dt);
        }

        /// <summary>Velocity of a point rigidly attached to a body: v = v_ref + ω × (p − p_ref).</summary>
        public static Vector3 PointVelocity(Vector3 referenceVelocity, Vector3 angularVelocity, Vector3 referencePoint, Vector3 point)
        {
            return referenceVelocity + Vector3.Cross(angularVelocity, point - referencePoint);
        }
    }
}
