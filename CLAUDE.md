# CricketVR — Coding Agent Instructions

## Spec-driven development workflow

Use the specification package under `specs/001-six-ball-net/` when present:

- `spec.md` owns observable requirements, scope, and acceptance scenarios.
- `plan.md` owns technical decisions and architecture.
- `tasks.md` owns implementation order, dependencies, and requirement mappings.
- `validation.md` owns actual evidence, device checks, and unresolved limitations.
- This `CLAUDE.md` owns agent conduct and general project conventions. Its earlier product/technical detail is background; a more precise feature spec or plan governs the corresponding feature detail.

Explicit owner instructions take priority. If project documents conflict, identify the conflict and reconcile the documents before implementing the affected behavior. Do not silently choose whichever instruction is easier.

Before coding an active milestone, read its requirements, plan, tasks, and relevant acceptance scenarios. If a material behavior is missing, add a concrete proposed spec change and surface the decision. Resolve routine technical details using the existing scope and judgment. Do not introduce mandatory approval pauses for every task; an owner request to implement a milestone authorizes ordinary work on that milestone.

Follow `specify -> plan -> tasks -> implement -> validate` for each increment. Keep spec and plan current when a requirement or implementation decision changes. Record requirement IDs in task mappings and validation evidence. A completed coding task is not evidence that its headset acceptance criteria passed.

The initial package is a proposed baseline derived from the conversation, not a claim of independent owner approval for every tuning default. No Unity project or game implementation has been verified merely by creating these documents.

If these companion files are missing, ask the owner to copy the supplied spec-driven starter package into the project root before implementing the full feature. You may still inspect tooling and report readiness.

## 1. Project and owner

Build a small, standalone VR cricket batting game for the owner's Meta Quest 3.

The owner understands programming and cricket but is a beginner in Unity and VR development. Explain Unity concepts when they first matter. Provide precise, version-appropriate Editor instructions for work that cannot be automated.

Development hardware: **MacBook Air M4, 16 GB RAM**. Target hardware: **Meta Quest 3 with Touch Plus controllers**. The game executes on the headset; the Mac is the development and build machine.

Working project name: **CricketVR**. CricketX is a reference for the type of experience, not a source of code, art, branding, or assets.

### Product goal

> Put on the headset, stand at the crease, press Start, face six deliveries, see the results, and play again.

Prioritize reliable bat–ball contact, readable deliveries, and a satisfying batting response. Use primitive geometry until that loop works.

### How to use this file

- Treat this as the implementation brief and working conventions for the repository.
- Explicit owner instructions take precedence over these defaults.
- Inspect the actual repository and installed tooling before changing anything. Do not assume the project already exists or that packages are installed.
- When asked to begin implementation, start at the earliest incomplete milestone. Do not start implementing merely because you were asked to review this file.
- Complete the code, configuration, and checks available to you before requesting a specific headset or Editor action from the owner.

## 2. MVP scope

### Required

- One practice net, pitch, crease, and set of striker's stumps.
- A stationary batting position with natural head movement; no artificial locomotion.
- A virtual bat attached to the selected controller, with configurable alignment offsets.
- Right- and left-handed batting selection before a session.
- A ball launcher rather than an animated bowler.
- Configurable delivery speed, line, and length, with a repeatable default delivery.
- Ball flight under gravity, pitch bounce, bat contact, and stump contact.
- Shot response influenced by bat contact-point velocity, face orientation, and contact position.
- A complete six-delivery practice session, including misses and bowled deliveries.
- Simple, explicitly labeled **practice scoring**, plus runs, wickets, contacts, and misses.
- Start, readiness countdown, between-ball interval, results, and restart.
- Impact audio and modest controller vibration.
- Pausing on focus/tracking loss and reliable cleanup on restart.

### Deferred

Do not add these unless the owner requests them:

- Animated bowlers, fielders, crowds, or stadiums.
- Realistic fielding, running between wickets, catches, LBW, extras, or full innings rules.
- Swing, seam, spin, pitch types, or professional training claims.
- Multiplayer, online accounts, cloud saves, leaderboards, analytics services, or payments.
- Databases, backend servers, AI models, LLMs, or machine learning.
- Hand-tracking batting, mixed reality, replay systems, or advanced coaching analysis.
- Physical bat attachment requirements. Controller-only play must work first.

## 3. Technology decisions

| Layer | Default choice |
| --- | --- |
| Engine | A supported Unity 6 LTS release compatible with the selected XR packages |
| Language | C# |
| Rendering | Universal Render Pipeline (URP), simple mobile-friendly materials |
| XR provider | Unity OpenXR Plugin, configured for Meta Quest and Touch controllers |
| Input | Unity Input System |
| Player rig and menus | Unity XR Interaction Toolkit (XRI), XR Origin, controller-based UI |
| Physics | Unity built-in 3D physics with small custom cricket/contact calculations |
| UI | World-space Unity UI and TextMeshPro |
| Deployment | Android/Meta Quest build profile; Meta Quest Developer Hub (MQDH) |
| Build toolchain | Unity Hub Android Build Support, SDK, NDK, and OpenJDK matched to the Editor |
| Editor | VS Code or the owner's existing C# editor |
| Version control | Git with Unity text serialization and visible metadata |
| Persistence | Session state in memory; optional local settings only if needed |

### Package discipline

- First inspect `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, and `Packages/packages-lock.json` if they exist.
- Verify compatibility against official Unity/Meta documentation before selecting or changing versions. Record the exact Editor and package versions in the README.
- Use the native Apple Silicon Unity Editor on this Mac. Check that its macOS requirement is met.
- Use OpenXR for a new project. Do not follow legacy Oculus Integration/Oculus XR setup tutorials without checking their relevance.
- XRI is the default interaction framework. Do not add a second rig or duplicate input framework.
- Meta XR Core SDK can be added if a specific required Meta feature or setup needs it. Do not add the entire All-in-One SDK or Meta Interaction SDK merely because a tutorial includes it.
- If a template imports extra demo content, retain only what the prototype uses.
- Avoid third-party physics frameworks, dependency injection frameworks, ECS/DOTS, networking, and custom render pipelines.
- Commit package manifests and locks. Avoid package upgrades during a physics milestone unless necessary to fix a demonstrated issue.

## 4. Mac-to-Quest development workflow

Meta supports building and deploying Quest apps from macOS. **Meta Horizon Link's direct headset preview is Windows-only**; do not make it a prerequisite or suggest buying a Windows computer for this MVP.

Workflow:

1. Install the supported Unity Editor and its Android modules through Unity Hub.
2. Configure the owner's Meta developer account and enable headset Developer Mode if not already done.
3. Connect the headset using a USB-C cable that supports data, and have the owner accept device debugging authorization.
4. Configure OpenXR, the Quest build target, controller interaction profile, and required project validation settings.
5. Build an ARM64 standalone Android application and install it on the Quest using Unity Build and Run or MQDH.
6. Test actual batting on-device, inspect logs, adjust, and repeat.

Meta XR Simulator on Apple Silicon can help with scene and UI checks when compatible with the chosen versions. It is optional and cannot establish whether real batting/tracking feels correct.

Additional rules:

- Build in a local writable project folder, outside folders that continuously sync generated Unity data.
- Find the actual Unity executable and project paths instead of assuming an exact version path.
- Do not run a command-line Unity instance against a project already open in another Editor instance.
- Never claim a successful headset test based on an Editor simulation or a successful APK build.
- After installation, the standalone game must be playable without a cable or development computer.
- App-store publication and platform account features are outside this prototype's scope.

## 5. Scene, units, and coordinates

- Use **1 Unity unit = 1 metre**. Use m/s internally; convert the bowling-speed display from km/h using `m/s = km/h / 3.6`.
- Use +Y up. Define +Z from the striker toward the ball launcher; deliveries initially travel toward -Z. +X is the batter's right while facing the launcher.
- Put a named `BattingAnchor` at the striker position and keep the world, pitch, and rig arrangement consistent with it.
- Use a regulation-length pitch as a scale reference; launcher release position is separately configurable and is not synonymous with pitch length.
- Use a floor-relative tracking origin and a clear reference marker for the batting position. Do not hardcode player eye height.
- Start with an approximately 0.036 m radius ball and plausible bat dimensions; expose tuning in assets.
- Keep collider sizes and transforms consistent with metre scale; do not compensate for scale bugs using extreme velocities or masses.
- Keep the scoreboard out of the delivery sight line and comfortably readable from the stance.
- Keep the environment stationary. No camera shake, forced head rotation, or artificial camera movement.

Suggested hierarchy, adapted to the installed XRI version:

```text
Main
  Environment
    Pitch
    NetFrameAndVisuals
    NetBoundaryColliders
    Stumps
    BattingAnchor
    LauncherAnchor
    PracticeScoringZones
  Player
    XR Origin
      Camera Offset
        Main Camera
        Left Controller
        Right Controller
    BatVisual
    BatPhysicsProxy
  Gameplay
    SessionManager
    BowlingMachine
    ScoreManager
  UI
    StartPanel
    LiveScoreboard
    ResultsPanel
  Audio
```

Use inexpensive boundary colliders rather than a simulated cloth net. Visual netting does not need strand-by-strand collision geometry.

## 6. Code organization and ownership

Keep the architecture small. Use a namespace such as `CricketVR`, serialized Inspector references, plain C# data types, and direct events where useful. Do not introduce a global event bus, service locator, or persistent singleton system.

Create folders as needed rather than prebuilding empty scaffolding:

```text
Assets/CricketVR/
  Scenes/
  Scripts/
    Session/
    Batting/
    Bowling/
    Ball/
    Scoring/
    UI/
    Editor/
  Prefabs/
  Settings/
  Materials/
  Audio/
  Tests/
    EditMode/
    PlayMode/
Docs/
```

### Main responsibilities

| Component | Owns |
| --- | --- |
| `SessionManager` | Session state, six-delivery count, sequencing, pause, restart, active delivery ID |
| `BowlingMachine` | Selecting delivery configuration and calculating initial ball state |
| `BatController` | Controller pose, handedness, calibration, physics proxy, velocity sampling |
| `BallController` | Ball flight lifecycle, collisions, reset, timeout, contact reporting |
| `BatBallContactResolver` | Contact detection/validation and outgoing ball velocity if complexity warrants a separate class |
| `ScoreManager` | Pure scoring decisions and aggregated session results |
| `ScoreboardUI` | Displaying state without deciding gameplay outcomes |
| `FeedbackController` | Optional small component for sounds and haptics |

Use a small `GameConfig`/`DeliveryConfig` ScriptableObject for exposed settings. Suggested fields: delivery count (default 6), launch speed, release pose, line/length target, countdown, between-ball delay, ball timeout, bounce parameters, bat response tuning, scoring zones, and contact debug toggles.

`DeliveryResult` should carry a unique delivery ID, outcome, runs, whether contact occurred, whether bowled, and measured shot data. Preserve a delivery configuration or random seed when needed to reproduce a bug. Session results are derived from the finalized delivery records.

Prefer ordinary events like `DeliveryStarted`, `BatContact`, `DeliveryFinalized`, and `SessionCompleted` only where they simplify ownership. Subscribe/unsubscribe consistently. A UI refresh must never change the score.

## 7. Session rules and state machine

Use an explicit state enum and guarded transitions:

```text
Ready -> Countdown -> DeliveryActive -> Resolving -> BetweenBalls
                                                   -> Countdown (next ball)
                                                   -> SessionComplete (after ball 6)
SessionComplete -> Ready (Restart)
```

This sketch describes sequencing; implement the branch from resolution appropriately. `Paused` and tracking recovery must be supported without accidentally scheduling another delivery.

Rules:

- There are exactly **six finalized playable deliveries** in a completed session.
- A wicket does not end this practice session. Continue until six deliveries are complete.
- A miss and a bowled ball count as deliveries. There are no wides, no-balls, or replacement balls for poor batting.
- Only one ball can be active. The previous ball must be disabled/reset before another launches.
- One delivery can finalize only once, even if several colliders report it in the same frame.
- Reject stale events whose delivery/session identity no longer matches the active delivery.
- A delivery finishes on a valid terminal outcome, boundary exit, settling, or bounded timeout. Bat contact alone does not necessarily finish it.
- Evaluate contacts chronologically. A confirmed stump strike is bowled and awards zero; never award runs for a delivery already finalized as bowled.
- A miss is a delivery without bat contact or a wicket. A bat contact followed by bowled is a contact and a wicket, not a miss.
- Restart must cancel pending countdowns, coroutines/tasks, timeouts, and delayed callbacks; clear results; reset the ball; and return to Ready.
- Do not automatically resume bowling after the app loses focus or tracking. Pause, invalidate the interrupted active delivery without consuming a ball, clean up, and ask the player to confirm readiness before retrying it.
- Treat cancellation/retry differently from a missed ball. Previously completed deliveries remain intact.
- Store all runtime session data in memory. No file persistence is needed for scores.

## 8. Bat tracking and collision design

This is the project's highest-risk gameplay area. Test it before visual polish.

### Tracking and alignment

- Use the selected controller's tracked pose. Define a bat grip anchor and adjustable local position/rotation offsets.
- Support handedness by changing the controller binding and intended alignment; do not blindly mirror the entire XR rig or use negative collider scale.
- Keep the visual bat responsive to tracking. Drive a separate kinematic Rigidbody collider proxy at physics time if necessary.
- Use appropriate Rigidbody movement APIs for the proxy, not unchecked transform teleports on a dynamic body.
- Keep a short timestamped pose history. Compute velocity over real sample intervals rather than assuming render and physics ticks coincide.
- Detect recentering, tracking jumps, and recovery. Clear history and suppress contact until valid samples exist, so a tracking discontinuity cannot launch the ball.
- Expose alignment adjustment before sessions. Simple Inspector offsets are acceptable initially; a basic in-game alignment control can follow.

### Fast contact handling

- Start with a sphere collider/dynamic Rigidbody for the ball and a box or simple compound collider for the bat.
- Select CCD modes supported by the actual body types and Unity version. Kinematic and rotating objects require special care.
- CCD is a starting point, not proof of reliability. Sweep-based CCD can miss rotation-driven contacts; speculative CCD can produce missed or false contacts.
- If real or scripted swings reveal tunneling, add a focused swept/relative-motion contact check with temporal subdivision where needed.
- A ball-only sphere cast against the bat's latest pose does not fully handle a rotating bat moving through the ball between samples. Account for both objects' motion, and test bat-tip arcs.
- Handle initial overlaps explicitly; casts alone may not report them.
- Use dedicated physics layers and bounded queries. Ignore scoring/UI/player geometry in ball contact calculations as appropriate.
- Route callback and sweep detections through one contact resolver so the same encounter cannot apply two impulses.
- Record only the first bat strike per delivery for MVP. Suppress repeated overlap impulses; still detect subsequent ground/stump/net contacts.
- If overriding the outgoing ball velocity, ensure the default physics solver does not also add an uncontrolled bat bounce. Document whether the resolver or solver owns bat impact response.

### Shot response

Use the bat's velocity **at the contact point**, including rotation:

`v_contact = v_reference + angularVelocity × (contactPoint - referencePoint)`

Keep all vectors in a consistent world space. Quaternion delta handling must use the shortest rotation, correct time interval, and radians per second.

Start with an understandable relative-velocity/normal response, then tune bounded restitution, tangential transfer, and off-centre response. Do not choose shot direction from swing speed alone.

Expected behavior:

- A stationary face can deflect an incoming ball.
- A soft defensive movement reduces outgoing speed compared with an attacking swing.
- A faster well-aligned swing generally sends the ball faster.
- Opening/closing the face changes direction; loft changes launch angle.
- Off-centre contact behaves less cleanly than a central strike without random, unexplained launches.
- Every result has finite position/velocity values and stays within documented tuning bounds.

Measure before reducing the fixed timestep aggressively. Render refresh rate and physics frequency are independent. Profile the selected physics rate on Quest, and document any change from defaults.

## 9. Bowling and bounce

- Begin with one repeatable slow-to-medium straight delivery, then expose controls. A starting range of approximately 60–100 km/h is a tuning choice, not a realism guarantee.
- Store release position, launch speed, line target, and length target separately.
- Gravity and ball physics should determine flight after release. Do not move the ball along an animation while also applying Rigidbody physics.
- For a specified bounce point and release speed, calculate a physically consistent initial velocity or solve numerically. Detect unreachable combinations and report them rather than silently changing the requested speed.
- Measure actual bounce location in a repeatable test. A field named `length` must affect bounce location, not just rename a preset.
- Tune ground restitution/friction consistently. Keep seam and spin disabled for MVP.
- Reset linear/angular velocity, trail, contact flags, timers, and physics state between balls. Prevent spawn overlap and unintended residual motion.
- Reset trails/history after teleports so there is no line across the pitch or false swept collision.

## 10. Practice scoring and distance

This is an arcade practice system, not an implementation of match fielding rules.

Suggested configurable defaults at the enclosure:

| Result/zone | Practice runs |
| --- | ---: |
| Miss or bowled | 0 |
| Side/rear zone following bat contact | 1 |
| Front lower zone following bat contact | 2 |
| Front middle target following bat contact | 4 |
| Front upper target following bat contact | 6 |

- Label these as practice runs in the UI/help. Do not present an upper-net hit as a real six.
- Define nonoverlapping zone bounds, deterministic tie-breaking, and an explicit zero-run fallback for a hit that reaches no scoring zone before timeout.
- Finalize only at the appropriate terminal event. An unhit delivery striking a zone never scores runs.
- Net walls stop the ball in this MVP. A scoring trigger should not intercept it earlier than intended physical contact.
- Track `Balls faced`, `Practice runs`, `Bat contacts`, `Misses`, and `Bowled`.
- For distance, measure horizontal displacement from bat contact to first post-contact ground landing, only when a real landing occurs before the net stops the ball. Label this `Measured carry`.
- If the ball hits the net first, show `Net hit`/`N/A` for carry. Never report the net distance as a 48 m shot.
- Estimated unobstructed carry is deferred. If later implemented, clearly label it as estimated and account for the chosen flight model.

## 11. Eight implementation milestones

Work in order. Keep each milestone playable and its changes reviewable.

### M1 — First scene on Quest

Build: inspect/setup tooling, create the URP project, configure OpenXR and input, add XR Origin, floor, simple objects, and controller visuals. Build and install.

Acceptance: launches on Quest 3; correct stereo rendering and head movement; floor scale is plausible; both controllers are tracked; no blocking errors.

Evidence: exact versions, build result, and a headset checklist. If you cannot operate the headset, mark headset validation as pending and give the owner precise steps.

### M2 — Practice net

Build: pitch, crease, stumps, launcher anchor, net enclosure, and readable lighting using primitives. Establish coordinates and metre scale.

Acceptance: the owner can stand at the crease and see a correctly proportioned lane; no UI obstructs the incoming ball; floor/stance alignment works.

### M3 — Tracked bat

Build: selected-controller bat, alignment offsets, handedness selection, physics proxy, and pose sampling. No ball needed yet.

Acceptance: stance, defensive movement, and full swings track consistently; left/right selection works; recentering creates no velocity spikes; no duplicate controller/bat rigs.

### M4 — Bowling machine

Build: one reusable ball, repeatable launches, gravity, bounce, delivery configuration, and a bounded reset loop. Include desktop debug launch controls.

Acceptance: repeated identical configurations produce a consistent bounce area; speed units are correct; changing line/length changes delivery behavior; only one ball is active.

### M5 — Batting contact

Build: contact detection, outgoing response, impact audio/haptics, and debug contact markers. Focus on physics rather than visuals.

Acceptance: stationary blocks, drives, lofted shots, off-centre hits, and fast swings respond plausibly; no repeated impulses or obvious tunneling in the agreed test set. Verify on Quest before calling batting validated.

### M6 — Delivery outcomes

Build: wicket/miss/contact detection, terminal rules, practice zones, distance handling, and per-delivery records.

Acceptance: every playable delivery finalizes exactly once; bowled awards zero; unhit zone contacts do not score; timeout and outside-boundary paths terminate cleanly; distances are honestly labeled.

### M7 — Six-ball loop

Build: Ready/Start, countdown, six deliveries, intervals, live scoreboard, results, and Restart. Integrate pause and interrupted-delivery recovery.

Acceptance: exactly six results; wickets do not end the session early; totals match results; repeated restart has no stale balls or callbacks; focus loss cannot secretly consume a ball.

### M8 — Quest polish and verification

Build: improve ball contrast, menu readability, sound balance, modest haptics, error recovery, and on-device performance.

Acceptance: three consecutive sessions and repeated restarts work; both handedness settings work; pause/recovery works; no known blocker; on-device frame timing meets the selected refresh target during normal play.

## 12. Performance and comfort

- Start with a **72 Hz on-device rendering target**. Treat it as a measured target: approximately 13.9 ms frame budget, with CPU and GPU assessed separately.
- Use Unity Profiler and appropriate Quest metrics. Mac Editor performance cannot establish headset performance.
- Keep lighting simple, bake static lighting if useful, limit realtime shadows, and avoid post-processing until justified.
- Prefer primitive/simple colliders and shared materials; use mobile-suitable shaders.
- One ball, one bat, and static surroundings should remain inexpensive. Investigate unexpected overhead before adding optimization frameworks.
- Avoid per-frame allocations in tracking/contact paths, repeated object searches, and unbounded debug logs. Cache references.
- Preserve the headset's boundary system. Do not auto-recenter the player during a live ball.
- Include a short readiness reminder to clear swing space. Test initially with controllers; a physical bat is not a prototype requirement.

## 13. Verification strategy

Add focused tests for meaningful failure modes, not tests that merely restate trivial assignments.

### EditMode candidates

- km/h-to-m/s conversion and delivery calculation bounds.
- Scoring rules, tie-breaking, wicket zero-score handling, and no-contact no-score behavior.
- Six-delivery counting, duplicate result rejection, stale delivery rejection, and restart state reset.
- Contact math: stationary face, angular contact velocity, finite outputs, and bounded response.

### PlayMode candidates

- Repeated launch/reset without residual ball state.
- Scripted bat sweeps at several speeds/angles, including a moving bat and ball, rotation-dominated tip motion, and initial overlap.
- State transitions, cancellation during countdown, and restart during/after a delivery.
- No duplicate impulse when sweep and collision callback detect the same encounter.

### Headset checklist

1. Correct scale and floor alignment.
2. Natural bat alignment for both hands.
3. Stationary blocks, drives, lofted attempts, and fast swings.
4. A miss, a bowled ball, a hit into each score zone, and timeout fallback.
5. Exactly six results, correct totals, and restart.
6. Focus/tracking interruption and explicit readiness recovery.
7. Several sessions without stale input, extra balls, runaway velocities, or frame-time problems.

Expose a deterministic debug delivery and optional contact point, normal, velocity, bounce point, and delivery ID visualizations. Disable verbose instrumentation in normal builds.

## 14. Repository and coding conventions

- Use the Unity version's supported APIs; check signatures instead of copying obsolete tutorial code. For example, Unity 6 Rigidbody APIs can differ from older examples.
- Keep runtime code and Editor automation separate. Editor code belongs under an Editor folder or Editor-only assembly.
- Prefer explicit serialized references and `[RequireComponent]` where helpful; validate missing scene dependencies with actionable errors.
- Use clear names, small components, and comments explaining non-obvious physics or timing decisions.
- Keep Unity lifecycle methods thin. Separate testable calculations from MonoBehaviour wiring when useful.
- Avoid hand-writing large Unity scene/prefab YAML. Prefer Editor tooling or a small idempotent setup script, with clear invocation instructions.
- A setup tool must not overwrite owner-edited scenes, create duplicate rigs, or reset project settings on every run.
- Keep `.meta` files when assets move. Commit `Assets`, `Packages`, and `ProjectSettings`.
- Ignore generated `Library`, `Temp`, `Obj`, `Logs`, build outputs, and local IDE artifacts. Never delete `Assets` or project settings as a troubleshooting shortcut.
- Use Git LFS only if asset sizes justify it. Do not introduce it for a primitives-only prototype.
- Do not download paid/unlicensed assets, expose secrets, commit signing credentials, publish the game, or change developer-account ownership.

## 15. Agent working process

At the start of an implementation task:

1. Inspect repository instructions, current milestone status, Unity version, packages, scenes, and Git state.
2. Identify the smallest playable change that advances the active milestone.
3. State the intended outcome briefly and implement it.
4. Run available compilation/tests/build checks appropriate to the change.
5. Update setup/run documentation and milestone status with evidence.
6. Report what works, what was actually tested, and the next concrete action.

Continue autonomous work within the requested scope. Do not repeatedly ask for permission for ordinary code edits or reversible setup. Ask only when an actual missing choice or owner-controlled step blocks progress.

When Unity/headset access is unavailable, complete source changes and automation that can be prepared safely. Provide exact remaining Editor/device steps, distinguish static review from compilation and device validation, and leave physical validation pending. Do not claim a milestone is fully passed without its required evidence.

When asking the owner to use the Editor, include the menu path, object/component names, field values, and expected visible result. Match the installed Unity/XRI version; do not invent UI labels.

Maintain a short `Docs/PROGRESS.md` once implementation begins:

| Milestone | Implementation | Automated/build checks | Quest validation | Blocker/next action |
| --- | --- | --- | --- | --- |
| M1–M8 | Not started / In progress / Implemented | Actual evidence or Pending | Actual evidence or Pending | Concrete next step |

Keep the README sufficient for the owner to reopen the project, install dependencies, build to Quest, and resume work after a break. Record important physics choices and known limitations concisely.

## 16. Definition of done

The MVP is complete only when:

- The standalone app launches and plays on the owner's Quest 3.
- Bat tracking, alignment, and tested high-speed contact are reliable enough for the prototype.
- The player can complete six deliveries with meaningful hit/miss/wicket outcomes.
- Results, practice runs, and distance labels match what the system actually measured.
- Three consecutive sessions, restart, and interruption recovery behave correctly.
- The selected on-device performance target is verified with evidence.
- Setup, controls, build instructions, versions, and remaining limitations are documented.

## 17. Official references

Check the documentation matching the installed versions; these links are starting points, not a frozen version contract.

- Meta hardware setup and Mac/Windows testing routes: https://developers.meta.com/vr/documentation/unity/unity-env-device-setup/
- Meta manual Unity/OpenXR project setup: https://developers.meta.com/vr/documentation/unity/unity-tutorial-hello-world-manual-steps/
- Meta deployment and testing: https://developers.meta.com/vr/documentation/unity/unity-deploy-and-test/
- Meta Link development limitations: https://developers.meta.com/vr/documentation/unity/unity-link/
- Unity XR Interaction Toolkit: https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@latest/
- Unity OpenXR Plugin: https://docs.unity3d.com/Packages/com.unity.xr.openxr@latest/
- Unity 6 system requirements: https://docs.unity3d.com/6000.0/Documentation/Manual/system-requirements.html
- Unity continuous collision detection: https://docs.unity3d.com/6000.0/Documentation/Manual/ContinuousCollisionDetection.html

## 18. First implementation action

When the owner says to start, inspect their Mac/project and begin **M1: get a simple tracked VR scene running on the Quest 3**. Keep the project small and verify deployment before implementing cricket gameplay.
