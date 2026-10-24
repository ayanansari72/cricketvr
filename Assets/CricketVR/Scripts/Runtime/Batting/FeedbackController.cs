using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace CricketVR
{
    /// <summary>
    /// Impact sound and controller vibration that depend on HOW the ball met the bat (owner feedback, E-022/E-023):
    ///   middle (sweet spot) - the owner's recorded bat hits; one firm, clean pulse
    ///   off-centre (toe, shoulder, handle) - the same hits made duller and deader; a jolt then a fading buzz
    ///   edge - only the bright attack of the hits (a nick); two quick ticks
    /// A contact that is partly middle and partly edge blends the sounds. Harder impacts are louder and slightly
    /// brighter and vibrate more. Clips come from Tools/audio/derive_bat_sounds.py (three variants per kind).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class FeedbackController : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] BatBallContactResolver resolver;

        [SerializeField] AudioClip[] middleClips;
        [SerializeField] AudioClip[] offCentreClips;
        [SerializeField] AudioClip[] edgeClips;

        AudioSource _source;
        int _variant;
        Coroutine _haptics;

        void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.minDistance = 1f;
            _source.maxDistance = 30f;
            // Mostly 2D: the bat is always within arm's reach, and a fully 3D source was too quiet (E-023).
            _source.spatialBlend = 0.4f;
            if (middleClips == null || middleClips.Length == 0)
                Debug.LogError("[CricketVR] FeedbackController has no impact clips; run CricketVR > Setup > 6. Build or Repair Main Scene.", this);
        }

        void OnEnable()
        {
            if (resolver != null) resolver.BatContact += OnBatContact;
        }

        void OnDisable()
        {
            if (resolver != null) resolver.BatContact -= OnBatContact;
        }

        void OnBatContact(BatContactReport report)
        {
            if (config == null)
                return;
            var c = config.contact;
            float strength = Mathf.Clamp01(report.Response.ApproachSpeed / Mathf.Max(1f, c.fullImpactSpeed));

            // How the contact splits between middle, off-centre and edge (weights sum to 1).
            float edge = Mathf.Abs(report.Response.EdgeFactor);
            float middle = report.Response.SweetFactor * (1f - edge);
            float dull = (1f - report.Response.SweetFactor) * (1f - edge);

            transform.position = report.Point;
            _variant++;
            float loudness = Mathf.Lerp(0.45f, 1f, strength);
            _source.pitch = 0.97f + 0.06f * strength; // harder hits sound a little brighter
            // A middled shot rings out loudest for the same impact; an edge is the quietest.
            Play(middleClips, loudness * middle);
            Play(offCentreClips, loudness * dull * 0.9f);
            Play(edgeClips, loudness * edge * 0.8f);

            var usage = report.Hand == BatHand.Right ? UnityEngine.InputSystem.CommonUsages.RightHand : UnityEngine.InputSystem.CommonUsages.LeftHand;
            if (InputSystem.GetDevice<XRController>(usage) is XRControllerWithRumble controller)
            {
                if (_haptics != null) StopCoroutine(_haptics);
                _haptics = StartCoroutine(Vibrate(controller, middle, dull, edge, strength, c));
            }
        }

        void Play(AudioClip[] clips, float volume)
        {
            if (clips == null || clips.Length == 0 || volume < 0.02f)
                return;
            var clip = clips[_variant % clips.Length];
            if (clip != null)
                _source.PlayOneShot(clip, volume);
        }

        /// <summary>
        /// Vibration pattern for the dominant kind of contact, scaled by impact strength. The kinds differ in RHYTHM
        /// and length, not only strength, so they can be told apart in the hand (owner feedback, E-023):
        ///   middle      one firm, clean 40 ms pulse
        ///   off-centre  a full-strength jolt, then a buzz that fades over ~0.25 s (the sting of a mistimed shot)
        ///   edge        two quick sharp ticks
        /// </summary>
        static IEnumerator Vibrate(XRControllerWithRumble controller, float middle, float dull, float edge, float strength, ContactSettings c)
        {
            float lo = c.hapticMinAmplitude, hi = c.hapticMaxAmplitude;
            float amp = Mathf.Lerp(lo, hi, strength);
            if (edge >= middle && edge >= dull)
            {
                controller.SendImpulse(amp, 0.02f);
                yield return new WaitForSecondsRealtime(0.05f);
                controller.SendImpulse(amp * 0.8f, 0.02f);
            }
            else if (dull > middle)
            {
                controller.SendImpulse(1f, 0.06f);
                yield return new WaitForSecondsRealtime(0.06f);
                foreach (float level in new[] { 0.8f, 0.6f, 0.45f, 0.3f })
                {
                    controller.SendImpulse(Mathf.Max(lo, amp) * level, 0.05f);
                    yield return new WaitForSecondsRealtime(0.05f);
                }
            }
            else
            {
                controller.SendImpulse(amp, 0.04f);
            }
        }

        public void Configure(GameConfig cfg, BatBallContactResolver contactResolver, AudioClip[] middle, AudioClip[] offCentre, AudioClip[] edge)
        {
            config = cfg;
            resolver = contactResolver;
            middleClips = middle;
            offCentreClips = offCentre;
            edgeClips = edge;
        }
    }
}
