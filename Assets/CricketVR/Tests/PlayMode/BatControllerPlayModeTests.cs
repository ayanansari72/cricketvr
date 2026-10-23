using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CricketVR.Tests
{
    /// <summary>
    /// Drives a stand-in controller transform (no XR device) to check the real BatController wiring:
    /// the kinematic proxy follows the grip, swings yield velocity, and teleports never do.
    /// </summary>
    public class BatControllerPlayModeTests
    {
        GameObject _root;
        Transform _right, _left;
        BatController _bat;
        Rigidbody _proxy;
        GameConfig _config;

        int _previousFrameRate;

        [SetUp]
        public void SetUp()
        {
            // Match the headset's 72 Hz so per-frame motion and the 0.1 s suppression window are realistic.
            _previousFrameRate = Application.targetFrameRate;
            Application.targetFrameRate = 72;
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _root = new GameObject("TestBatRig");
            _right = new GameObject("Right Controller").transform;
            _left = new GameObject("Left Controller").transform;
            _right.SetParent(_root.transform);
            _left.SetParent(_root.transform);

            var batGo = new GameObject("Bat");
            batGo.SetActive(false); // configure before Awake runs
            batGo.transform.SetParent(_root.transform);
            var visual = new GameObject("BatVisual").transform;
            visual.SetParent(batGo.transform);
            var proxyGo = new GameObject("BatPhysicsProxy");
            proxyGo.transform.SetParent(batGo.transform);
            _proxy = proxyGo.AddComponent<Rigidbody>();
            var box = proxyGo.AddComponent<BoxCollider>();

            _bat = batGo.AddComponent<BatController>();
            _bat.Configure(_config, _left, _right, visual, null, _proxy, box, requireTracked: false);
            batGo.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Application.targetFrameRate = _previousFrameRate;
            Object.Destroy(_root);
            Object.Destroy(_config);
        }

        [UnityTest]
        public IEnumerator ProxyFollowsGripPoseThroughPhysics()
        {
            _right.SetPositionAndRotation(new Vector3(0.2f, 1.0f, 1.1f), Quaternion.Euler(-30f, 10f, 0f));
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            var g = _config.bat.geometry;
            var expected = _bat.GripPose.position + _bat.GripPose.rotation * g.BladeCenter;
            Assert.That(Vector3.Distance(_proxy.position, expected), Is.LessThan(0.002f));
            Assert.That(Quaternion.Angle(_proxy.rotation, _bat.GripPose.rotation), Is.LessThan(0.5f));
            Assert.IsTrue(_proxy.isKinematic);
            Assert.AreEqual(CollisionDetectionMode.ContinuousSpeculative, _proxy.collisionDetectionMode);
        }

        [UnityTest]
        public IEnumerator SteadySwingGivesToeVelocityIncludingRotation()
        {
            // Rotate the controller about world Y at 10 rad/s around its own position for ~0.3 s.
            const float omega = 10f;
            float angle = 0f;
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < 0.3f)
            {
                angle += omega * Time.unscaledDeltaTime * Mathf.Rad2Deg;
                _right.SetPositionAndRotation(new Vector3(0f, 1f, 1f), Quaternion.Euler(0f, angle, 0f) * Quaternion.Euler(-90f, 0f, 0f));
                yield return null;
            }

            Assert.IsTrue(_bat.ContactAllowed);
            var toe = _bat.GripPose.position + _bat.GripPose.rotation * _config.bat.geometry.Toe;
            Assert.IsTrue(_bat.TryGetPointVelocity(toe, out var v));
            float radius = Vector3.ProjectOnPlane(toe - _bat.GripPose.position, Vector3.up).magnitude;
            Assert.AreEqual(omega * radius, v.magnitude, omega * radius * 0.1f, "toe speed should be ω·r");
        }

        [UnityTest]
        public IEnumerator TeleportResetsHistoryAndSuppressesContacts()
        {
            // Slow drift for 0.3 s: enough samples and past the start-up suppression window.
            float start = Time.unscaledTime;
            for (int i = 0; Time.unscaledTime - start < 0.3f; i++)
            {
                _right.position = new Vector3(0f, 1f, 1f + i * 0.005f);
                yield return null;
            }
            Assert.IsTrue(_bat.ContactAllowed);
            int resets = _bat.Motion.ResetCount;

            // Simulated recentre: the controller jumps 2 m in one frame.
            _right.position += new Vector3(2f, 0f, 0f);
            yield return null;

            Assert.Greater(_bat.Motion.ResetCount, resets);
            Assert.IsFalse(_bat.ContactAllowed);
            Assert.IsFalse(_bat.TryGetPointVelocity(_right.position, out _));
        }
    }
}
