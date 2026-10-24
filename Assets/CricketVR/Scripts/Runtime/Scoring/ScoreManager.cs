using System;
using System.Collections.Generic;
using UnityEngine;

namespace CricketVR
{
    /// <summary>
    /// Turns ball, bat and bowling events into one <see cref="DeliveryResult"/> per playable delivery (plan.md §6).
    /// Decisions live in <see cref="DeliveryAdjudicator"/>; this component only gathers events and keeps the records.
    ///
    /// Timing: collision callbacks only queue events. Each physics step, before the contact resolver (-40) can apply
    /// a bat hit, this component (-45) hands the previous simulation step's events to the adjudicator, so a bat
    /// contact is never credited to a collision that happened before it. When a result is decided the ball is
    /// made dead: it lingers visibly but can't be hit or change the result.
    /// </summary>
    [DefaultExecutionOrder(-45)]
    public class ScoreManager : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] BallController ball;
        [SerializeField] BatBallContactResolver resolver;
        [SerializeField] BowlingMachine machine;

        DeliveryAdjudicator _adjudicator;
        readonly List<BallEvent> _pending = new List<BallEvent>(8);
        readonly List<DeliveryResult> _results = new List<DeliveryResult>(8);
        int _pitchLayer, _stumpsLayer;
        int _lastDecidedId = -1;

        public IReadOnlyList<DeliveryResult> Results => _results;
        public SessionTotals Totals => SessionTotals.From(_results);
        public event Action<DeliveryResult> DeliveryFinalized;

        void Awake()
        {
            if (config == null || ball == null)
            {
                Debug.LogError("[CricketVR] ScoreManager is missing references; run CricketVR > Setup > 6. Build or Repair Main Scene.", this);
                enabled = false;
                return;
            }
            _adjudicator = new DeliveryAdjudicator(config.scoring.rules);
            _pitchLayer = LayerMask.NameToLayer("Pitch");
            _stumpsLayer = LayerMask.NameToLayer("Stumps");
        }

        void OnEnable()
        {
            if (ball != null)
            {
                ball.Collided += OnCollided;
                ball.DeliveryEnded += OnDeliveryEnded;
            }
            if (resolver != null) resolver.BatContact += RecordBatContact;
            if (machine != null) machine.Bowled += OnBowled;
        }

        void OnDisable()
        {
            if (ball != null)
            {
                ball.Collided -= OnCollided;
                ball.DeliveryEnded -= OnDeliveryEnded;
            }
            if (resolver != null) resolver.BatContact -= RecordBatContact;
            if (machine != null) machine.Bowled -= OnBowled;
        }

        /// <summary>Clears the records (session restart). Any open delivery is discarded without a result.</summary>
        public void ClearResults()
        {
            if (_adjudicator != null && _adjudicator.IsOpen)
                _adjudicator.Cancel(_adjudicator.DeliveryId);
            _pending.Clear();
            _results.Clear();
        }

        void OnBowled(int id, DeliveryConfig request, DeliverySolution solution)
        {
            _pending.Clear();
            _adjudicator.Begin(id, request);
        }

        void FixedUpdate()
        {
            if (_adjudicator == null)
                return;
            int id = ball.ActiveDeliveryId;
            EnsureBegun(id);
            if (!_adjudicator.IsOpen || _adjudicator.DeliveryId != id)
            {
                _pending.Clear();
                return;
            }

            if (ball.IsLive)
            {
                var exit = PracticeScoring.ExitSide(ball.Body.position, config.scoring.exitMargin);
                if (exit.HasValue)
                    _pending.Add(BallEvent.Exit(exit.Value, ball.Body.position));
            }
            if (_pending.Count == 0)
                return;
            if (_adjudicator.ProcessStep(id, _pending, out var result))
                Decide(result);
            _pending.Clear();
        }

        /// <summary>A ball launched without the bowling machine (tests, future callers) still gets a result.</summary>
        void EnsureBegun(int id)
        {
            if (ball.IsLive && id == ball.ActiveDeliveryId && id != _lastDecidedId && (!_adjudicator.IsOpen || _adjudicator.DeliveryId != id))
            {
                _pending.Clear();
                _adjudicator.Begin(id, default);
            }
        }

        void OnCollided(int id, Collision collision)
        {
            if (_adjudicator == null || !_adjudicator.IsOpen || id != _adjudicator.DeliveryId)
                return; // stale or already decided
            var point = collision.GetContact(0).point;
            var go = collision.collider.gameObject;
            if (collision.collider.TryGetComponent<NetWall>(out var wall))
                _pending.Add(BallEvent.Net(wall.side, point));
            else if (go.layer == _stumpsLayer)
                _pending.Add(BallEvent.Stumps(point));
            else if (go.layer == _pitchLayer)
                _pending.Add(BallEvent.Ground(point));
        }

        /// <summary>A bat contact applied by <see cref="BatBallContactResolver"/> (subscribed automatically).</summary>
        public void RecordBatContact(BatContactReport report)
        {
            if (_adjudicator == null)
                return;
            EnsureBegun(report.DeliveryId);
            _adjudicator.RecordBatContact(report.DeliveryId, new ShotData
            {
                ContactPoint = report.Point,
                ExitVelocity = report.BallVelocityOut,
                ContactHeightFromToe = report.HeightFromToe,
            });
        }

        void OnDeliveryEnded(int id, BallEndReason reason)
        {
            if (_adjudicator == null)
                return;
            switch (reason)
            {
                case BallEndReason.Cancelled:
                    // Relaunched or cancelled before a result: not a playable delivery, no record.
                    if (_adjudicator.IsOpen && _adjudicator.DeliveryId == id && config.scoring.logResults)
                        Debug.Log($"[CricketVR] Delivery #{id} cancelled before a result; no record");
                    _adjudicator.Cancel(id);
                    _pending.Clear();
                    break;
                case BallEndReason.Settled:
                case BallEndReason.Timeout:
                    if (_adjudicator.EndWithoutTerminal(id, reason == BallEndReason.Settled ? DeliveryEnd.Settled : DeliveryEnd.Timeout, out var result))
                        Decide(result);
                    break;
            }
        }

        void Decide(DeliveryResult result)
        {
            _lastDecidedId = result.DeliveryId;
            ball.Finish(result.DeliveryId, config.scoring.deadBallLinger);
            _results.Add(result);
            if (config.scoring.logResults)
                Debug.Log($"[CricketVR] Result {result} || {Totals}");
            DeliveryFinalized?.Invoke(result);
        }

        public void Configure(GameConfig cfg, BallController ballController, BatBallContactResolver contactResolver, BowlingMachine bowlingMachine)
        {
            config = cfg;
            ball = ballController;
            resolver = contactResolver;
            machine = bowlingMachine;
        }
    }
}
