using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace CricketVR
{
    /// <summary>
    /// Owns the bat: which controller holds it, grip alignment, the visual, the kinematic physics proxy,
    /// and the pose history used for contact-point velocity. Contacts are only trustworthy while
    /// <see cref="ContactAllowed"/> is true; any tracking discontinuity clears history first.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class BatController : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [Tooltip("Transform driven by the LEFT controller's grip pose (devicePosition/deviceRotation), not the aim pose.")]
        [SerializeField] Transform leftController;
        [Tooltip("Transform driven by the RIGHT controller's grip pose (devicePosition/deviceRotation), not the aim pose.")]
        [SerializeField] Transform rightController;
        [Tooltip("Root of the visual bat; re-parented under the selected controller at runtime.")]
        [SerializeField] Transform batVisual;
        [SerializeField] Renderer bladeRenderer;
        [SerializeField] Rigidbody physicsProxy;
        [SerializeField] BoxCollider proxyCollider;
        [Tooltip("Off only for automated tests without an XR device.")]
        [SerializeField] bool requireTrackedState = true;

        static readonly Color SuppressedTint = new Color(1f, 0.35f, 0.3f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        // v5: owner-aligned defaults baked into GameConfig (2026-10-23); older saved nudges no longer apply.
        const string PrefsPrefix = "CricketVR.Grip.v5.";

        BatMotionTracker _tracker;
        BatHand _hand = BatHand.Right;
        readonly GripAlignment[] _alignment = new GripAlignment[2];
        InputAction _leftTracked, _rightTracked;
        readonly List<XRInputSubsystem> _inputSubsystems = new List<XRInputSubsystem>();
        bool _originChanged, _focusChanged;
        MaterialPropertyBlock _tintBlock;
        Color _bladeColor = Color.white;
        bool _tinted;
        bool _hasPose; // FixedUpdate can run before the first Update has sampled a pose

        // Swing logging (evidence for AC-03): peak toe speed between rest periods.
        bool _inSwing;
        float _peakToeSpeed, _peakOmega;

        public event Action<MotionResetReason> MotionReset;

        public BatHand Hand => _hand;
        public BatGeometry Geometry => config.bat.geometry;
        public GripAlignment Alignment => _alignment[(int)_hand];
        public BatMotionTracker Motion => _tracker;
        public bool ContactAllowed => _tracker != null && _tracker.ContactAllowed(Time.unscaledTimeAsDouble);
        public Pose GripPose { get; private set; }
        Transform Controller(BatHand hand) => hand == BatHand.Right ? rightController : leftController;

        void Awake()
        {
            if (config == null || leftController == null || rightController == null || batVisual == null || physicsProxy == null || proxyCollider == null)
            {
                Debug.LogError("[CricketVR] BatController is missing references; run CricketVR > Setup > 6. Build or Repair Main Scene.", this);
                enabled = false;
                return;
            }

            _tracker = new BatMotionTracker(config.bat.motionLimits, 16);
            _alignment[(int)BatHand.Right] = LoadAlignment(BatHand.Right, config.bat.rightHandGrip);
            _alignment[(int)BatHand.Left] = LoadAlignment(BatHand.Left, config.bat.leftHandGrip);

            physicsProxy.isKinematic = true;
            physicsProxy.useGravity = false;
            physicsProxy.interpolation = RigidbodyInterpolation.None;
            // Speculative CCD is the only continuous mode Unity supports for kinematic bodies.
            physicsProxy.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            proxyCollider.center = Vector3.zero;
            proxyCollider.size = Geometry.BladeSize;

            if (bladeRenderer != null)
            {
                _tintBlock = new MaterialPropertyBlock();
                _bladeColor = bladeRenderer.sharedMaterial.HasProperty(BaseColorId) ? bladeRenderer.sharedMaterial.GetColor(BaseColorId) : Color.white;
            }
            ApplyHand(_hand, MotionResetReason.HandChanged);
        }

        void OnEnable()
        {
            _leftTracked = new InputAction("LeftTracked", InputActionType.PassThrough, "<XRController>{LeftHand}/isTracked");
            _rightTracked = new InputAction("RightTracked", InputActionType.PassThrough, "<XRController>{RightHand}/isTracked");
            _leftTracked.Enable();
            _rightTracked.Enable();

            SubsystemManager.GetSubsystems(_inputSubsystems);
            foreach (var s in _inputSubsystems)
                s.trackingOriginUpdated += OnTrackingOriginUpdated;
            Application.focusChanged += OnFocusChanged;
        }

        void OnDisable()
        {
            _leftTracked?.Dispose();
            _rightTracked?.Dispose();
            foreach (var s in _inputSubsystems)
                s.trackingOriginUpdated -= OnTrackingOriginUpdated;
            _inputSubsystems.Clear();
            Application.focusChanged -= OnFocusChanged;
        }

        void OnTrackingOriginUpdated(XRInputSubsystem _) => _originChanged = true;
        void OnFocusChanged(bool _) => _focusChanged = true;

        public bool IsControllerTracked(BatHand hand)
        {
            if (!requireTrackedState)
                return true;
            var action = hand == BatHand.Right ? _rightTracked : _leftTracked;
            return action != null && action.ReadValue<float>() > 0.5f;
        }

        void Update()
        {
            double now = Time.unscaledTimeAsDouble;

            // Events from other threads/callbacks are applied here so the history changes in one place.
            if (_originChanged) { _originChanged = false; ResetMotion(MotionResetReason.OriginChanged); }
            if (_focusChanged) { _focusChanged = false; ResetMotion(MotionResetReason.FocusChanged); }

            var controller = Controller(_hand);
            LocalGrip(out var localPosition, out var localRotation);
            GripPose = new Pose(controller.TransformPoint(localPosition), controller.rotation * localRotation);
            _hasPose = true;

            bool tracked = IsControllerTracked(_hand);
            var reason = _tracker.AddSample(now, GripPose.position, GripPose.rotation, tracked);
            if (reason != MotionResetReason.None)
                NotifyReset(reason);

            UpdateSwingLog();
            UpdateTint();
        }

        void FixedUpdate()
        {
            bool usable = _hasPose && IsControllerTracked(_hand);
            if (proxyCollider.enabled != usable)
                proxyCollider.enabled = usable;
            if (!usable)
                return;

            // Kinematic bodies must be moved with MovePosition/MoveRotation so the physics engine
            // sees the motion (and speculative CCD can use it), rather than teleporting the transform.
            var pose = GripPose;
            physicsProxy.MovePosition(pose.position + pose.rotation * Geometry.BladeCenter);
            physicsProxy.MoveRotation(pose.rotation);
        }

        /// <summary>World velocity of a point on the bat, including rotation. False while motion is untrusted.</summary>
        public bool TryGetPointVelocity(Vector3 worldPoint, out Vector3 velocity)
        {
            velocity = Vector3.zero;
            if (!ContactAllowed || !_tracker.TryGetVelocity(out var linear, out var angular))
                return false;
            velocity = PoseHistory.PointVelocity(linear, angular, _tracker.History.Latest.Position, worldPoint);
            return true;
        }

        public void SetBatHand(BatHand hand)
        {
            if (hand == _hand)
                return;
            ApplyHand(hand, MotionResetReason.HandChanged);
            Debug.Log($"[CricketVR] Bat hand: {hand}");
        }

        /// <summary>Adjusts the current hand's grip by whole steps on each axis (angles wrap). Persists locally.</summary>
        public void NudgeAlignment(int tiltSteps, int faceSteps, int leanSteps = 0, int slideSteps = 0, int spinSteps = 0)
        {
            var s = config.bat;
            var a = _alignment[(int)_hand];
            a.tilt = Mathf.DeltaAngle(0f, a.tilt + tiltSteps * s.alignmentStepDegrees);
            a.faceTurn = Mathf.DeltaAngle(0f, a.faceTurn + faceSteps * s.alignmentStepDegrees);
            a.lean = Mathf.DeltaAngle(0f, a.lean + leanSteps * s.alignmentStepDegrees);
            a.slide = Mathf.Clamp(a.slide + slideSteps * s.slideStep, -0.3f, 0.3f);
            a.spin = Mathf.DeltaAngle(0f, a.spin + spinSteps * s.alignmentStepDegrees);
            SetAlignment(a);
        }

        /// <summary>Grip-to-bat transform in the controller's local frame, including the owner's offsets.</summary>
        void LocalGrip(out Vector3 position, out Quaternion rotation)
        {
            var a = Alignment;
            rotation = BatFrame.GripRotation(_hand, a.tilt, a.faceTurn, a.lean, a.faceFlipped, a.spin);
            position = a.position + rotation * (Vector3.up * a.slide);
        }

        /// <summary>Swaps the hitting face and the back, keeping the bat's angle.</summary>
        public void FlipFace()
        {
            var a = _alignment[(int)_hand];
            a.faceFlipped = !a.faceFlipped;
            SetAlignment(a);
        }

        public void ResetAlignment() => SetAlignment(_hand == BatHand.Right ? config.bat.rightHandGrip : config.bat.leftHandGrip);

        void SetAlignment(GripAlignment a)
        {
            _alignment[(int)_hand] = a;
            SaveAlignment(_hand, a);
            ApplyVisualPose();
            // Changing the grip offset moves the bat instantly: that is not a swing.
            ResetMotion(MotionResetReason.Manual);
            Debug.Log($"[CricketVR] {_hand} grip alignment: tilt {a.tilt:F0}, face {a.faceTurn:F0}, lean {a.lean:F0}, spin {a.spin:F0} deg, slide {a.slide * 100f:F0} cm, face flipped {a.faceFlipped}");
        }

        public void ResetMotion(MotionResetReason reason)
        {
            _tracker.Reset(reason, Time.unscaledTimeAsDouble);
            NotifyReset(reason);
        }

        void ApplyHand(BatHand hand, MotionResetReason reason)
        {
            _hand = hand;
            batVisual.SetParent(Controller(hand), false);
            ApplyVisualPose();
            ResetMotion(reason);
        }

        void ApplyVisualPose()
        {
            // The visual is a child of the controller, so it follows the late (onBeforeRender) pose update.
            LocalGrip(out var position, out var rotation);
            batVisual.localPosition = position;
            batVisual.localRotation = rotation;
        }

        void NotifyReset(MotionResetReason reason)
        {
            _inSwing = false;
            Debug.Log($"[CricketVR] Bat motion reset: {reason}; contacts suppressed for {config.bat.motionLimits.suppressAfterReset:F2} s");
            MotionReset?.Invoke(reason);
        }

        void UpdateSwingLog()
        {
            if (!config.bat.logSwings || !_tracker.TryGetVelocity(out var linear, out var angular))
                return;
            var toe = GripPose.position + GripPose.rotation * Geometry.Toe;
            float toeSpeed = PoseHistory.PointVelocity(linear, angular, _tracker.History.Latest.Position, toe).magnitude;

            if (!_inSwing && toeSpeed > 3f)
            {
                _inSwing = true;
                _peakToeSpeed = 0f;
                _peakOmega = 0f;
            }
            if (_inSwing)
            {
                _peakToeSpeed = Mathf.Max(_peakToeSpeed, toeSpeed);
                _peakOmega = Mathf.Max(_peakOmega, angular.magnitude);
                if (toeSpeed < 1f)
                {
                    _inSwing = false;
                    Debug.Log($"[CricketVR] Swing ({_hand} hand): peak toe {_peakToeSpeed:F1} m/s, peak angular {_peakOmega:F1} rad/s, contact allowed {ContactAllowed}");
                }
            }
        }

        void UpdateTint()
        {
            if (bladeRenderer == null || !config.bat.showSuppressionTint)
                return;
            bool tint = !ContactAllowed;
            if (tint == _tinted)
                return;
            _tinted = tint;
            bladeRenderer.GetPropertyBlock(_tintBlock);
            _tintBlock.SetColor(BaseColorId, tint ? SuppressedTint : _bladeColor);
            bladeRenderer.SetPropertyBlock(_tintBlock);
        }

        static GripAlignment LoadAlignment(BatHand hand, GripAlignment fallback)
        {
            var key = PrefsPrefix + hand;
            fallback.tilt = PlayerPrefs.GetFloat(key + ".tilt", fallback.tilt);
            fallback.faceTurn = PlayerPrefs.GetFloat(key + ".face", fallback.faceTurn);
            fallback.lean = PlayerPrefs.GetFloat(key + ".lean", fallback.lean);
            fallback.slide = PlayerPrefs.GetFloat(key + ".slide", fallback.slide);
            fallback.spin = PlayerPrefs.GetFloat(key + ".spin", fallback.spin);
            fallback.faceFlipped = PlayerPrefs.GetInt(key + ".flipped", fallback.faceFlipped ? 1 : 0) != 0;
            return fallback;
        }

        static void SaveAlignment(BatHand hand, GripAlignment a)
        {
            var key = PrefsPrefix + hand;
            PlayerPrefs.SetFloat(key + ".tilt", a.tilt);
            PlayerPrefs.SetFloat(key + ".face", a.faceTurn);
            PlayerPrefs.SetFloat(key + ".lean", a.lean);
            PlayerPrefs.SetFloat(key + ".slide", a.slide);
            PlayerPrefs.SetInt(key + ".flipped", a.faceFlipped ? 1 : 0);
            PlayerPrefs.SetFloat(key + ".spin", a.spin);
            PlayerPrefs.Save();
        }

        /// <summary>Wires references from code (scene builder and tests).</summary>
        public void Configure(GameConfig cfg, Transform left, Transform right, Transform visual, Renderer blade,
            Rigidbody proxy, BoxCollider collider, bool requireTracked = true)
        {
            config = cfg;
            leftController = left;
            rightController = right;
            batVisual = visual;
            bladeRenderer = blade;
            physicsProxy = proxy;
            proxyCollider = collider;
            requireTrackedState = requireTracked;
        }
    }
}
