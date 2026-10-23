using UnityEngine;

namespace CricketVR
{
    /// <summary>
    /// Applies the batter's handedness before a session: rig position (stance marker), scoreboard side,
    /// and the default bat controller. Never mirrors the rig or uses negative scale.
    /// Changing handedness moves the player, so it must only happen outside a live delivery (M7 enforces this).
    /// </summary>
    public class HandednessSetup : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] BatController bat;
        [Tooltip("The XR Origin transform.")]
        [SerializeField] Transform xrOrigin;
        [SerializeField] Transform rightHandedStance;
        [SerializeField] Transform leftHandedStance;
        [Tooltip("Scoreboard placed for a right-hander; mirrored across the pitch centre line for a left-hander.")]
        [SerializeField] Transform scoreboard;

        const string PrefsKey = "CricketVR.Handedness";

        Pose _rightHandedScoreboard;

        public Handedness Current { get; private set; }

        void Start()
        {
            if (config == null || bat == null || xrOrigin == null || rightHandedStance == null || leftHandedStance == null)
            {
                Debug.LogError("[CricketVR] HandednessSetup is missing references; run CricketVR > Setup > 6. Build or Repair Main Scene.", this);
                enabled = false;
                return;
            }
            if (scoreboard != null)
                _rightHandedScoreboard = new Pose(scoreboard.position, scoreboard.rotation);

            var saved = (Handedness)PlayerPrefs.GetInt(PrefsKey, (int)config.defaultHandedness);
            Apply(saved, chooseBatHand: true);
        }

        public void Toggle() => Set(Current == Handedness.Right ? Handedness.Left : Handedness.Right);

        public void Set(Handedness handedness)
        {
            if (handedness == Current)
                return;
            Apply(handedness, chooseBatHand: true);
            PlayerPrefs.SetInt(PrefsKey, (int)handedness);
            PlayerPrefs.Save();
        }

        void Apply(Handedness handedness, bool chooseBatHand)
        {
            Current = handedness;

            var stance = handedness == Handedness.Right ? rightHandedStance : leftHandedStance;
            xrOrigin.SetPositionAndRotation(new Vector3(stance.position.x, 0f, stance.position.z), Quaternion.identity);

            if (scoreboard != null)
            {
                var p = _rightHandedScoreboard;
                if (handedness == Handedness.Left)
                {
                    // Mirror across x = 0 (the stump line): negate x, and reflect the yaw.
                    var e = p.rotation.eulerAngles;
                    p = new Pose(new Vector3(-p.position.x, p.position.y, p.position.z), Quaternion.Euler(e.x, -e.y, -e.z));
                }
                scoreboard.SetPositionAndRotation(p.position, p.rotation);
            }

            if (chooseBatHand)
            {
                // Prefer the conventional bottom hand, but don't switch to a controller that isn't tracking.
                var preferred = BatFrame.DefaultHandFor(handedness);
                if (bat.IsControllerTracked(preferred))
                    bat.SetBatHand(preferred);
            }

            // Moving the rig moves the controllers in world space: never treat that as bat motion.
            bat.ResetMotion(MotionResetReason.HandChanged);
            Debug.Log($"[CricketVR] Handedness: {handedness}; bat in {bat.Hand} hand");
        }

        public void Configure(GameConfig cfg, BatController batController, Transform origin, Transform rightStance, Transform leftStance, Transform board)
        {
            config = cfg;
            bat = batController;
            xrOrigin = origin;
            rightHandedStance = rightStance;
            leftHandedStance = leftStance;
            scoreboard = board;
        }
    }
}
