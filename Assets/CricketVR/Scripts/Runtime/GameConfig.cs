using System;
using UnityEngine;

namespace CricketVR
{
    [Serializable]
    public struct GripAlignment
    {
        [Tooltip("Grip point offset in the controller's local frame (m).")]
        public Vector3 position;
        [Tooltip("Rotation about the blade's width axis (degrees, -180..180).")]
        public float tilt;
        [Tooltip("Turn about the untilted handle axis, applied before tilt: swings the direction the tilt points (degrees, -180..180).")]
        public float faceTurn;
        [Tooltip("Rotation about the face normal, leaning the bat sideways (degrees, -180..180).")]
        public float lean;
        [Tooltip("Slides the bat along its length in the hand (m); positive moves it toward the handle end.")]
        public float slide;
        [Tooltip("Swap the hitting face and the back without changing the bat's angle.")]
        public bool faceFlipped;
        [Tooltip("Twist about the bat's own (tilted) handle axis, applied last (degrees, -180..180).")]
        public float spin;
    }

    [Serializable]
    public class BatSettings
    {
        public BatGeometry geometry = BatGeometry.Default;
        public BatMotionLimits motionLimits = BatMotionLimits.Default;
        public GripAlignment rightHandGrip;
        public GripAlignment leftHandGrip;

        [Header("In-headset alignment nudges")]
        [Tooltip("Degrees per thumbstick step.")]
        public float alignmentStepDegrees = 5f;
        [Tooltip("Metres per slide step.")]
        public float slideStep = 0.01f;

        [Header("Debug")]
        [Tooltip("Tint the blade red while contacts are suppressed after a tracking discontinuity.")]
        public bool showSuppressionTint = true;
        [Tooltip("Log a summary of each swing (peak toe speed, peak angular speed) to the device log.")]
        public bool logSwings = true;

        public GripAlignment GripFor(BatHand hand) => hand == BatHand.Right ? rightHandGrip : leftHandGrip;
    }

    /// <summary>Tunable game settings. One asset: Assets/CricketVR/Settings/GameConfig.asset.</summary>
    [CreateAssetMenu(menuName = "CricketVR/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        public Handedness defaultHandedness = Handedness.Right;
        public BatSettings bat = new BatSettings();
    }
}
