using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace CricketVR
{
    /// <summary>
    /// Debug bowling until the session flow exists (M7):
    ///   tap A (or F key)   bowl the selected preset again
    ///   tap B (or N key)   select the next preset and bowl it
    /// A tap is a press shorter than 0.4 s, so it doesn't clash with the 1 s holds used by the bat setup controls.
    /// A world-space readout on the scoreboard shows the preset and the last measured bounce.
    /// </summary>
    public class BowlingDebugControls : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] BowlingMachine machine;
        [SerializeField] ScoreManager score;
        [SerializeField] TMP_Text readout;
        public bool allowDebugInput = true;

        const float TapSeconds = 0.4f;

        InputAction _primary, _secondary, _bowlKey, _nextKey;
        float _primaryDown = -1f, _secondaryDown = -1f;
        int _preset;
        string _lastResult = "Tap A to bowl, tap B for the next delivery";
        string _lastOutcome = "";

        DeliveryConfig[] Presets => config.delivery.debugPresets;

        void OnEnable()
        {
            _primary = new InputAction("BowlTap", InputActionType.Button, "<XRController>/{PrimaryButton}");
            _secondary = new InputAction("NextTap", InputActionType.Button, "<XRController>/{SecondaryButton}");
            _bowlKey = new InputAction("BowlKey", InputActionType.Button, "<Keyboard>/f");
            _nextKey = new InputAction("NextKey", InputActionType.Button, "<Keyboard>/n");
            _primary.Enable();
            _secondary.Enable();
            _bowlKey.Enable();
            _nextKey.Enable();
            if (machine != null)
            {
                machine.BounceMeasured += OnBounce;
                machine.Rejected += OnRejected;
            }
            if (score != null)
                score.DeliveryFinalized += OnResult;
            Refresh();
        }

        void OnDisable()
        {
            _primary?.Dispose();
            _secondary?.Dispose();
            _bowlKey?.Dispose();
            _nextKey?.Dispose();
            if (machine != null)
            {
                machine.BounceMeasured -= OnBounce;
                machine.Rejected -= OnRejected;
            }
            if (score != null)
                score.DeliveryFinalized -= OnResult;
        }

        void Update()
        {
            if (!allowDebugInput || machine == null || config == null || Presets.Length == 0)
                return;

            if (Tapped(_primary, ref _primaryDown) || _bowlKey.WasPressedThisFrame())
                BowlSelected();
            if (Tapped(_secondary, ref _secondaryDown) || _nextKey.WasPressedThisFrame())
            {
                _preset = (_preset + 1) % Presets.Length;
                BowlSelected();
            }
        }

        void BowlSelected()
        {
            var preset = Presets[_preset];
            int id = machine.Bowl(preset);
            if (id != 0)
            {
                _lastResult = $"#{id} in flight...";
                _lastOutcome = "";
            }
            Refresh();
        }

        static bool Tapped(InputAction action, ref float downTime)
        {
            if (action.WasPressedThisFrame())
                downTime = Time.unscaledTime;
            if (action.WasReleasedThisFrame() && downTime >= 0f)
            {
                bool tap = Time.unscaledTime - downTime < TapSeconds;
                downTime = -1f;
                if (tap && action.activeControl?.device is XRControllerWithRumble c)
                    c.SendImpulse(0.25f, 0.04f);
                return tap;
            }
            return false;
        }

        void OnBounce(int id, Vector3 point, float error)
        {
            _lastResult = $"#{id} bounced at {point.z:F2} m, line {point.x:+0.00;-0.00;0.00} m ({error * 100f:F0} cm from target)";
            Refresh();
        }

        void OnResult(DeliveryResult r)
        {
            _lastOutcome = $"<b>#{r.DeliveryId} {r.Summary}</b>\n{r.CarryLabel}\n{score.Totals}";
            Refresh();
        }

        void OnRejected(DeliveryConfig request, string error)
        {
            _lastResult = "REJECTED: " + error;
            Refresh();
        }

        void Refresh()
        {
            if (readout == null || config == null || Presets.Length == 0)
                return;
            var p = Presets[_preset];
            readout.text = $"<b>DEBUG BOWLING</b>\n{_preset + 1}/{Presets.Length} {p.label}\n" +
                           $"{p.speedKmh:F0} km/h | length {p.length:F1} m | line {p.line:+0.00;-0.00;0.00} m\n{_lastResult}" +
                           (string.IsNullOrEmpty(_lastOutcome) ? "" : "\n" + _lastOutcome);
        }

        public void Configure(GameConfig cfg, BowlingMachine bowlingMachine, ScoreManager scoreManager, TMP_Text text)
        {
            config = cfg;
            machine = bowlingMachine;
            score = scoreManager;
            readout = text;
        }
    }
}
