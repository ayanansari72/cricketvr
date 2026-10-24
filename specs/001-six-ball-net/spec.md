# Feature 001: six-ball net session

Status: Proposed baseline; implementation not started.
Source: Owner's agreed MVP and milestones from the conversation.

## Goal and player story

As a cricket player, I want to stand at a virtual crease on my Quest 3, face six balls using a tracked bat, and review the session so that I can enjoy a short, repeatable batting experience.

Development machine: M4 MacBook Air, 16 GB RAM. Target: standalone Quest 3, controller-only play. Unity knowledge is beginner level. The game must run without the Mac after installation.

## Scope

One net, one pitch, striker's stumps, one tracked bat, one active ball, configurable launcher, practice scoring, scoreboard, and replaying the session. Include both handedness settings and interruption recovery.

Deferred: realistic fielders, catches, LBW, running between wickets, extras, animated bowlers, spin/swing/seam, stadiums, physical bat requirements, multiplayer, cloud services, coaching claims, and AI models.

## Requirements

| ID | Observable requirement | Milestone |
| --- | --- | --- |
| REQ-001 | The app installs from macOS and launches standalone on Quest 3 with stereo head tracking and both controllers tracked. | M1 |
| REQ-002 | The practice environment has consistent metre scale, floor alignment, visible crease/stumps, and an unobstructed view toward the launcher. | M2 |
| REQ-003 | The selected controller drives a virtual bat with adjustable alignment and right/left-handed selection before starting. | M3 |
| REQ-004 | The launcher produces repeatable deliveries with configurable speed, line, and bounce length; only one ball is active. | M4 |
| REQ-005 | Flight and bounce respond consistently to the configured launch state and pitch; impossible delivery settings are reported. | M4 |
| REQ-006 | Bat contact reflects contact-point velocity, orientation, and contact position, producing distinguishable blocks, drives, and lofted responses. | M5 |
| REQ-007 | The agreed fast-swing test set shows no observed tunneling or duplicate bat impulse; tracking discontinuities cannot create a shot. | M5 |
| REQ-008 | Every completed playable delivery produces exactly one final result: runs, contact state, wicket state, and available shot measurements. | M6 |
| REQ-009 | Runs come from labeled practice zones after contact; misses/bowled score zero and unhit zone contacts never score. | M6 |
| REQ-010 | Distance labels distinguish actual measured carry from net interruption; no unmeasured open-field distance is displayed. | M6 |
| REQ-011 | Start begins a readiness countdown, then a session of exactly six finalized deliveries with intervals between balls. | M7 |
| REQ-012 | Results show six delivery records and matching totals; a wicket does not end the session early. | M7 |
| REQ-013 | Restart returns to Ready with cleared scores, no active ball, and no stale countdowns/callbacks. | M7 |
| REQ-014 | Focus/tracking loss stops bowling, invalidates any interrupted active delivery without consuming it, and requires readiness confirmation before retry. | M7 |
| REQ-015 | Menus/scoreboard are readable from the stance; impacts produce sound and modest vibration; there is no forced camera movement. | M5/M8 |
| REQ-016 | Three consecutive sessions plus restart/interruption checks succeed on Quest at the selected measured rendering target. | M8 |
| REQ-017 | A ground mode replaces the practice net with a believable cricket ground seen from the striker's crease: outfield with mowing stripes, pitch strip with Laws-sized markings at both ends, both sets of stumps, boundary rope, 30-yard circle markers, sight screens, advertising boards, low stands with a crowd impression, sky and distance fog. | M6.5 |
| REQ-018 | All ground dimensions come from one `GroundDefinition` asset. The ground can be rebuilt from it reproducibly with an Editor menu, with no hand-placed geometry. | M6.5 |
| REQ-019 | The pitch keeps its collider and physics material, so delivery bounce calibration is unchanged. The outfield has its own thicker collider with lower bounce and higher friction, and a 45 m/s ball can't tunnel through it. A documented rolling drag slows the ball only on the outfield. | M6.5 |
| REQ-020 | The game detects a ball leaving the ground (over the rope or above the stands) once per delivery, raises `BallLeftGround(position, time)` without scoring, and hides the ball 1.5 s later. A safety despawn removes a ball that goes below the outfield or more than 150 m from the ground centre. | M6.5 |
| REQ-021 | A scene-level NetMode/GroundMode switch exists. NetMode is unchanged M6 behaviour. In GroundMode net scoring is off and every delivery still finalizes exactly once: Bowled (stumps, highest priority), Miss (unhit only: past z < −1 m at any x, or settles/times out unhit), or "Hit, practice scoring pending" (any hit, including edges behind the wicket). | M6.5 |
| REQ-022 | Static placeholder fielders stand at the positions in a `FieldConfig` list (angle and radius from the striker's stumps). They can be hidden and don't move or field. | M6.5 |
| REQ-023 | GroundMode holds the 72 Hz target on Quest 3 within these budgets: ≤ 150 draw calls, < 250k triangles, < 150 MB texture memory. Textures are ASTC with mipmaps. There are no large transparent surfaces and no animated environment. | M6.5 |
| REQ-024 | The ball stays high-contrast against the sight screen behind the machine, the pitch and the outfield. Thin distant lines (rope, circle markers, stands) don't visibly shimmer, and markings don't z-fight. | M6.5 |

## Proposed defaults and precise rules

These make the baseline implementable; they are not claims that every value was explicitly selected by the owner. Keep tuning configurable and document later changes.

- Delivery count: six. Three-second starting countdown and three-second inter-ball readiness interval; configurable.
- Default delivery: repeatable straight ball at approximately 80 km/h; line/length selected during M4 tuning. Start slower during initial batting tests.
- Ball timeout: bounded, initially 10 seconds after release; configurable.
- One wicket per delivery at most; it does not end the practice session.
- A miss means no bat contact and no wicket. Bat contact followed by bowled counts as a contact and a wicket, not a miss.
- A contact-only ball that reaches no scoring zone before settling/timeout receives zero practice runs.
- Runs default to side/rear = 1, front low = 2, front middle = 4, front upper = 6. Bowled/miss = 0. These are arcade practice zones, not real match boundaries.
- Zones must be nonoverlapping; at a shared edge, deterministic configured priority breaks a tie. A scoring zone requires prior valid bat contact on that delivery.
- First valid terminal event finalizes the delivery. Evaluate collisions in time order; bowled must not later gain zone runs. Do not award provisional runs before a terminal outcome.
- For a post-contact ground landing before net impact, measured carry is horizontal displacement from bat contact to landing. A prior net hit produces `Net hit / N/A`, not an invented carry.
- An interrupted delivery is cancelled and retried. Previously finalized records remain unchanged.
- MVP requires one accepted bat strike per delivery; overlap callbacks must not repeatedly accelerate the ball.
- Baseline render target: 72 Hz, approximately 13.9 ms frame budget. Changes require a recorded plan decision and matching validation.

### Cricket ground (M6.5, accepted 2026-10-24)

- **Ground centre and boundary:** ground centre (0, 0, 10.06). Boundary circle radius 65 m (allowed 55–75 m), with an optional ellipse (radius X and radius Z) for later.
- **Fielding circle:** the 30-yard circle is two 27.43 m semicircles centred on each middle stump, joined by straight lines and marked by discs about every 5 m.
- **Fielder angles:**
  - 0° points straight down the ground toward the machine (+Z), and positive angles are on the off side for a right-hander (+X).
  - The radius is measured from the striker's stumps.
  - World position = (r·sin θ, 0, r·cos θ).
  - For a left-hander, the angles mirror (θ → −θ).
- **Default field:**
  - Keeper 180°/12 m
  - Slip 160°/16 m
  - Point 95°/25 m
  - Cover 55°/28 m
  - Mid-off 20°/27 m
  - Mid-on −20°/27 m
  - Midwicket −60°/28 m
  - Square leg −95°/25 m
  - Third man 145°/50 m (GD-3)
  - Fine leg −150°/50 m (GD-3)
- GroundMode results (GD-8) and leaving the ground (GD-9) follow REQ-020 and REQ-021; decision record in `proposals/M6.5-cricket-ground.md`.

## Proposed changes pending owner approval

- **PC-01 (affects REQ-009, AC-06): zone geometry and boundary exit.** **Accepted by the owner on 2026-10-24** ("scoring rules look fine"); now part of REQ-009 scope.
  - Front scoring zones sit on the front net wall behind the launcher end and are split by impact height: low < 1.0 m scores 2, 1.0–3.0 m scores 4, above 3.0 m scores 6.
  - Side and rear walls score 1.
  - The enclosure has no roof. A contacted ball that leaves over the front wall scores 6. A contacted ball that leaves over a side or rear wall scores 1. An unhit ball leaving the enclosure scores 0. All of these are terminal.
  - The launcher has no collider, so returning balls pass through it.
  - Details are in `plan.md` §6.

## Acceptance scenarios

### AC-01 — On-device launch (REQ-001)

Given the project is built on the owner's Mac and installed on Quest, when the owner opens it independently, then the scene renders in stereo, head movement works, and both controller poses are available. The game does not depend on a connected Mac.

### AC-02 — Standing at the crease (REQ-002, REQ-015)

Given a valid floor-relative origin, when the player stands at the batting marker, then the crease, stumps, launcher, and net are proportionate and visible, the scoreboard does not obstruct delivery sight, and head motion is natural.

### AC-03 — Bat setup (REQ-003, REQ-007)

Given either handedness setting, when the player aligns and moves the bat, then it follows the selected controller consistently. After recentering or tracking recovery, pose history is reset and no false high-speed impact is possible.

### AC-04 — Reproducible delivery (REQ-004, REQ-005)

Given unchanged configuration and seed, when ten debug deliveries are launched separately, then exactly one ball is active at a time and bounce points remain within a 0.25 m radius of the first measured bounce point. Changing line/length changes the corresponding trajectory. An unreachable configuration produces an actionable error. This tolerance is a prototype default to review during M4.

### AC-05 — Contact matrix (REQ-006, REQ-007, REQ-015)

Given the deterministic delivery, test stationary-face block, soft defence, drive, loft, off-centre contact, and rotation-dominated bat-tip contact. Run ten scripted attempts for each applicable test at documented speeds/angles; no observed missed collision or duplicate impulse is allowed in that matrix. At comparable central contact, a faster aligned attack should produce more outgoing speed than soft defence. Repeat representative cases on Quest and have the owner assess feel. Scripted success does not imply all human swings are covered.

### AC-06 — Outcomes and honest metrics (REQ-008, REQ-009, REQ-010)

Given separate deliveries exercising miss, bowled, each score zone, no-contact zone entry, timeout, and net-first contact, then each finalizes once, totals follow the rules, and carry is shown only when actually measured. A contact then bowled is zero runs with both contact and wicket flags.

### AC-07 — Complete over (REQ-011, REQ-012)

Given Ready, when Start is pressed and six playable balls conclude, then the app shows the countdown, intervals, six result rows, and accurate totals, including any wickets. It launches no seventh ball.

### AC-08 — Restart (REQ-013)

Given countdown, active ball, between balls, or results, when Restart is invoked, then the app returns to Ready, clears session records, removes the active ball, and cancels old work. Repeat five times without stale callbacks affecting the next session.

### AC-09 — Interruption (REQ-014)

Given a countdown or active delivery, when app focus or required tracking is lost, then bowling stops. Any active incomplete delivery is cancelled without changing finalized results. After tracking returns, a player confirmation is required and the next attempt cannot have a velocity spike.

### AC-10 — Final device session (REQ-016)

Given the installed build and normal play conditions, complete three consecutive sessions, both-handedness checks, restart, and interruption cases. Record CPU/GPU frame timing against the target and any over-budget frames. Normal-play intervals should meet the selected frame budget; recurring misses are failures to investigate. Installation/loading time is reported separately. Pending checks cannot be marked passed.

### AC-11 — Standing in the middle (REQ-017, REQ-018, REQ-024)

Given GroundMode, when the owner stands at the crease, then:
- the ground looks true to scale;
- both sets of stumps and all creases are visible without flicker;
- the rope, circle markers and stands don't shimmer when the head moves;
- the sight screen sits directly behind the machine and the ball is easy to pick up at release.

### AC-12 — Ground physics (REQ-019, REQ-020)

Given GroundMode:
- **Bowling:** the ten-launch bounce check still lands within the AC-04 limits, and the automated calibration test still lands within 1 cm.
- **Outfield:** a ball driven along the ground slows to a stop on the outfield, and a ball dropped or hit at up to 45 m/s never falls through it.
- **Leaving the ground:** a ball lofted over the rope raises `BallLeftGround` exactly once, then disappears 1.5 s later.

### AC-13 — Mode switch (REQ-021, REQ-022)

Given NetMode, all M6 behaviour and every existing test are unchanged. Given GroundMode:
- each delivery shows exactly one result (bowled, miss, or "hit, practice scoring pending"); an unhit wide ball past the stumps is a miss; an edge going behind the wicket is "hit, practice scoring pending", never a miss;
- the fielders appear at their configured positions and hide when toggled.

### AC-14 — Ground performance (REQ-023)

Given GroundMode on Quest 3 during normal batting for at least 2 minutes:
- the frame rate holds 72 Hz with no sustained drops;
- draw calls ≤ 150 and triangles < 250k (Unity Profiler on a development build);
- texture memory < 150 MB;
- the measured numbers are recorded with fixed foveated rendering on and off (the estimate in plan.md is not the pass);
- CPU and GPU frame times are recorded separately.

## Completion rule

All requirements must have mapped tasks and acceptance evidence. If a criterion fails, keep it open or revise it transparently with the owner's scope/intent in view; do not silently weaken it to make a test pass.
