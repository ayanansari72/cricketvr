# Technical plan: six-ball net session

Status: Proposed baseline, 2026-10-23. No Unity project or code exists yet. Every value here is a starting default to be measured and tuned during its milestone. Record changes in the decisions log below.

Owns: technical decisions and architecture for `spec.md` (REQ-001..016). Task order lives in `tasks.md`. Evidence lives in `validation.md`.

## Inspected environment (T001, read-only, 2026-10-23)

- macOS 26.5.2 on Apple Silicon (M4 MacBook Air, 16 GB), ~150 GB free.
- `~/Desktop` is not iCloud-synced, so `/Users/nitish/Desktop/cricketvr` is acceptable as the project folder.
- Not installed: Unity Hub, Unity Editor, Android SDK/NDK, `adb`. Present: system `git` 2.50.1.
- The folder is not yet a Git repository. It contains only `CLAUDE.md`, `README.md`, and `specs/`.

## Conflicts and gaps found

1. `plan.md` was missing from the starter package. This document resolves that.
2. Forward shots travel +Z toward the launcher, so the "front" scoring zones must sit behind the launcher end. The spec doesn't define what happens when a ball clears a wall (no roof) or hits the launcher. §6 proposes rules; these are **pending owner approval** (see `spec.md`).
3. `README.md` says to copy these documents into an existing Unity project. Instead, the Unity project will be created in this folder (§2).

## 1. Toolchain and versions (T001–T002)
- **Unity Hub** (latest) → **Unity 6 LTS, newest patch** (expected to be 6000.3.x LTS; the exact version gets confirmed in Hub and the Meta/OpenXR compatibility notes at T002). Use the Apple Silicon Editor. Install these modules: **Android Build Support + OpenJDK + Android SDK & NDK Tools**, all managed by Hub. Don't use a system Java or SDK.
- Packages (record exact versions in README after resolution):
  - `com.unity.render-pipelines.universal` (from template)
  - `com.unity.xr.openxr`
  - `com.unity.xr.management`
  - `com.unity.inputsystem`
  - `com.unity.xr.interaction.toolkit` 3.x, plus only its **Starter Assets** sample (for XR Origin/controller input actions)
  - `com.unity.textmeshpro` / uGUI (built into Unity 6)
  - `com.unity.test-framework`
  - No Meta All-in-One SDK. Add Meta XR Core SDK only if a demonstrated need appears (none is planned).
- Owner-controlled steps (documented precisely in README):
  - Meta developer account/organization
  - Enable Developer Mode in the Horizon phone app
  - Use a USB-C **data** cable and accept "Allow USB debugging" in the headset
  - Install MQDH (optional; used for install, logs, and the performance HUD)

## 2. Project creation and repo layout (T003)
- Create the project in Hub with the **Universal 3D** template, *not* the VR template (avoids demo content), in a temporary sibling folder. Move `Assets/`, `Packages/`, `ProjectSettings/` into `/Users/nitish/Desktop/cricketvr`, then reopen from Hub. Reason: Hub won't create into a non-empty folder.
- `git init`. Add a Unity `.gitignore` (Library, Temp, Obj, Logs, UserSettings, Builds, `.vs*`, `.idea`, `*.apk`, `.DS_Store`). Set Editor settings to **Force Text** serialization and **Visible Meta Files**. No LFS.
- Assemblies (asmdefs) keep pure logic testable and Editor code separate:
  - `CricketVR.Core`: **no `UnityEngine` MonoBehaviours**. Only plain C# plus `UnityEngine` math types (Vector3, Quaternion). Holds the solvers, rules, and state machine.
  - `CricketVR.Runtime`: MonoBehaviours and ScriptableObjects. References Core, XRI, and Input System.
  - `CricketVR.Editor`: setup and build tooling, Editor-only.
  - `CricketVR.Tests.EditMode`: tests Core.
  - `CricketVR.Tests.PlayMode`: tests Runtime physics and session flow.
- Folders follow CLAUDE.md §6 and are created when first needed.

## 3. XR configuration (T003–T005)
- Build Profile: **Android** (Meta Quest profile if offered by the installed Editor). Settings:
  - ARM64 only, IL2CPP, min API per current Meta requirement (check at T002)
  - Vulkan graphics API
  - Linear color space
  - Texture compression ASTC
- XR Plug-in Management (Android tab) → **OpenXR**.
  - Feature group: **Meta Quest Support**.
  - Interaction profiles: **Oculus Touch Controller Profile** and **Meta Quest Touch Plus Controller Profile**.
  - Render mode: Single Pass Instanced / Multiview.
  - Set Tracking Origin to **Floor** on XR Origin.
- Run **Project Validation → Fix All**, then review what it changed in Git.
- Render target: **72 Hz** baseline. The display frequency is left at the default unless profiling requires otherwise.
- URP asset: no post-processing, no realtime shadows initially (or one soft-off directional shadow), MSAA 4x, HDR off.

## 4. Scene construction strategy
- One scene: `Assets/CricketVR/Scenes/Main.unity`.
- An **idempotent Editor menu** `CricketVR ▸ Setup ▸ Build/Repair Main Scene` (`Scripts/Editor/MainSceneBuilder.cs`) creates the missing objects from the CLAUDE.md §5 hierarchy by name. It never deletes or overwrites existing objects or their transforms. It logs what it created. XR Origin is instantiated from the XRI Starter Assets prefab **only if no `XROrigin` exists** (prevents duplicate rigs).
- `Scripts/Editor/QuestBuild.cs`: menu `CricketVR ▸ Build ▸ Quest APK` plus a `-executeMethod CricketVR.Editor.QuestBuild.Build` entry for batch builds. Batch builds only run when the Editor is closed. Output goes to `Builds/` (gitignored).
- A `SceneValidator` (runtime `OnValidate`/`Awake` checks) raises actionable errors for missing serialized references.

## 5. Coordinates and geometry (M2, T007–T008)
- 1 unit = 1 m. +Y up. Striker's **stumps base at origin**. Deliveries travel −Z, and +Z points toward the launcher. +X is the batter's right when facing the launcher, which is the off side for a right-hander.
- Pitch: 3.05 m wide, from z = −1.5 to z = +21.5, so the 20.12 m stump-to-stump reference is visible. Popping crease at z = +1.22, return creases at x = ±1.32.
- Stumps: 3 cylinders, 0.711 m tall, 0.2286 m total width, bails as visuals. A single **solid box collider** (layer `Stumps`) covers the stump set; M6 adds wicket handling on it (a trigger is only added if M6 needs one).
- `BattingAnchor`: the middle of the striker's popping crease (0, 0, 1.22). Floor stance markers: right-handed at (−0.3, 0, 1.0) (yellow), left-handed at (+0.3, 0, 1.0) (blue), i.e. on the leg side of the stumps just behind the crease. The XR Origin sits at the right-handed stance facing +Z, so the centre of the player's boundary maps there. Handedness switching (M3) moves the rig between markers before a session; never during a delivery.
- Non-striker stumps at z = 20.12 are visual only (depth cue behind the release point).
- `LauncherAnchor`: default release point (0, 2.0, 18.9), about at the bowler's popping crease, with height configurable. The launcher visual has **no collider**, so returning balls pass through.
- Net enclosure: inexpensive box colliders on a dedicated `Boundary` layer.
  - Side walls at x = ±1.8, 4 m high, z −3 → +24
  - Rear wall at z = −3
  - Front wall at z = +24, 6 m high
  - No roof
  - Visual net: tinted translucent quads (one shared URP Unlit transparent material, 35% alpha) plus dark poles and top rails. A fine alpha-tested grid was rejected because thin repeating lines alias/shimmer on Quest.
- Scoreboard: world-space board 1.0 × 0.6 m, about 2.4 m from the stance at 45° toward the off side, centre 1.9 m high, facing the player and inside the net (no collider). 3 m would put it through the side net. M2 places a blank placeholder; text arrives in M7. Start and results panels share that location. Handedness flips which side it uses.
- Lighting: one directional light (no shadows), flat ambient (0.55, 0.56, 0.60), URP Simple Lit materials. The outfield floor is a 50 m plane centred at z = 10. The ball is bright (white or high-visibility yellow), matching the contrast work in M8.

## 6. Practice zones and terminal rules (M6) — *proposed spec additions, flagged for owner*
- Zones are trigger volumes **just inside** each wall surface, about 5 cm thick. They only record "reached zone X at time t". The wall collider still physically stops the ball. Scoring is applied at that terminal event.
  - Front wall zones by impact height: **low < 1.0 m → 2**, **1.0–3.0 m → 4**, **> 3.0 m → 6**. Bounds are half-open intervals, so ties are deterministic.
  - Side walls and rear wall → **1**.
  - Tie priority if two zones fire in the same step: `FrontUpper > FrontMiddle > FrontLow > Side > Rear`.
- **Proposed new rule (gap):** a `BoundaryExit` volume encloses everything. If a contacted ball leaves over the front-wall top, it scores **6**. If it leaves over a side or rear wall, it scores **1**. An unhit ball exiting scores 0. All of these are terminal.
- Terminal events, finalize-once, earliest first:
  - stump strike → Bowled, 0 runs
  - zone reached after a valid bat contact → zone runs
  - zone reached without contact → Miss/NoContact, 0 runs, terminal
  - boundary exit
  - settled (speed < 0.2 m/s for 0.5 s) → 0 runs
  - timeout of 10 s after release → 0 runs
- Ground bounces are never terminal.
- Measured carry: on the first `Pitch/Ground` collision after bat contact, if no net hit came first, record horizontal |Δxz| from contact point to landing point. If a net hit came first, show `Net hit` and carry = N/A. Labelled "Measured carry".

## 7. Core architecture (classes, owners, data flow)

| Class | Assembly | Responsibility |
|---|---|---|
| `GameConfig` (SO) | Runtime | Delivery count 6, countdown 3 s, inter-ball 3 s, timeout 10 s, settle thresholds, bat tuning, zone runs/priority, debug toggles |
| `DeliveryConfig` (SO + serializable struct) | Runtime/Core | Release pos, speed km/h, line X at bounce, length (bounce Z), optional seed |
| `Units` | Core | `KmhToMs = v / 3.6` |
| `DeliverySolver` | Core | Release + bounce target + speed → initial velocity or `Unreachable` with reason |
| `PoseHistory` | Core | Ring buffer of (t, pos, rot). Linear/angular velocity over real intervals, plus point velocity |
| `BatContactMath` | Core | Relative-velocity/normal response, restitution vs. off-centre, clamps, finiteness |
| `SweptBatBallTest` | Core | Substepped relative-motion sphere-vs-OBB test |
| `ScoringRules` | Core | Zone → runs, priority, terminal decisions, carry rule |
| `DeliveryResult` / `SessionResults` | Core | Immutable records. Totals derived from records |
| `SessionStateMachine` | Core | Guarded transitions, generation counter, six-record limit |
| `BatController` | Runtime | Controller binding, grip offsets, visual, kinematic proxy, pose sampling, validity gating |
| `BallController` | Runtime | Ball lifecycle, launch/reset, collision → event routing with delivery ID |
| `BatBallContactResolver` | Runtime | **Sole authority** for bat impulse. Combines callback and sweep detections |
| `BowlingMachine` | Runtime | Picks config, calls solver, asks the ball to launch |
| `ScoreManager` | Runtime | Wraps ScoringRules. Receives ball events and emits `DeliveryFinalized` |
| `SessionManager` | Runtime | Drives the state machine with Update-based timers, handles pause/interrupts |
| `ScoreboardUI`, `StartPanel`, `ResultsPanel` | Runtime | Display only. Buttons call SessionManager |
| `FeedbackController` | Runtime | Impact audio + haptics |
| `DebugDeliveryPanel` | Runtime (dev builds) | Desktop keys / in-VR debug launch, markers |

Events are plain C# `event Action<…>` on owners: `DeliveryStarted(id)`, `BatContact(id, ContactData)`, `DeliveryFinalized(DeliveryResult)`, `SessionCompleted`. Subscribe in `OnEnable` and unsubscribe in `OnDisable`. There is no event bus and there are no singletons.

## 8. Bat tracking (M3, T010–T012)
- Use the XRI Starter Assets XR Origin. The controller GameObjects use `TrackedPoseDriver` (Input System) with the `isTracked`/`trackingState` actions.
- `BatController` binds to dedicated **Left Grip / Right Grip** transforms (TrackedPoseDriver on the grip pose `devicePosition`/`deviceRotation`; the Starter Assets controllers use the tilted aim pose and are kept for UI rays) based on `Handedness`, which is chosen on the Start panel before Ready. Grip offset: local position/rotation per hand, stored in `GameConfig` and editable in the Inspector and through a small in-VR nudge UI (T012). Mirror only the offset, never the rig or scales.
- **Visual bat** (blade 0.108 × 0.04 × 0.56 m, handle 0.30 m, primitives) is parented to the controller via the grip offset. It updates every frame, including at `onBeforeRender` through the TrackedPoseDriver.
- **Physics proxy**: a separate kinematic Rigidbody (BoxCollider = blade) on layer `BatProxy`. In `FixedUpdate` it calls `MovePosition/MoveRotation` toward the latest sampled pose. Interpolation is off, and CCD is `ContinuousSpeculative`, the only CCD mode valid for kinematic bodies.
- `PoseHistory` samples the grip pose in `Update` using `Time.unscaledTimeAsDouble` (pose at the frame the tracking was applied). It holds 16 samples.
  - Linear velocity comes from a least-squares or two-point fit over the last ~30 ms.
  - Angular velocity: `Δq = q1 * inverse(q0)`, force shortest path (w ≥ 0), `ToAngleAxis` → radians / real dt.
  - `v_point = v_ref + ω × (p − p_ref)`.
- **Discontinuity gating**: clear history and set `contactSuppressedUntil = now + 0.1 s` (plus ≥ 3 valid samples) when any of these happens:
  - `isTracked` becomes false
  - `XRInputSubsystem.trackingOriginUpdated` fires (recenter)
  - dt > 0.1 s
  - grip speed > 25 m/s or ω > 60 rad/s (tunable)
  - app focus is regained
  
  While suppressed, the resolver rejects all contacts.

## 9. Bowling and ball (M4, T013–T015)
- Ball: SphereCollider r = 0.036, mass 0.16 kg, dynamic Rigidbody, `ContinuousDynamic`, interpolation on, linear damping 0 (no drag in MVP, documented), layer `Ball`. Pitch PhysicMaterial: bounciness ~0.5, friction ~0.35 (tuned at M4). Combine modes are set so the pitch value dominates.
- `DeliverySolver`:
  - Displacement d = B − P0, horizontal D, vertical Δy (bounce point at y = r).
  - For speed s: `tanθ = (s² − √(s⁴ − g(gD² + 2Δy s²))) / (gD)`, taking the **lower** (flat) root.
  - A negative discriminant means Unreachable, reported with the minimum feasible speed. The speed is never silently changed.
  - Then **numeric refinement**: simulate the same semi-implicit Euler integrator at `Time.fixedDeltaTime` and shoot-adjust θ (bisection, ≤ 20 iterations) so the discrete trajectory lands at B within 1 cm. This removes the integrator bias.
- Launch: reset first, then `rb.position = P0`, zero velocities, `rb.linearVelocity = v0` (Unity 6 API), `trail.Clear()`, clear flags and timers, and assign a new `deliveryId` (monotonic int + session generation).
- The ball is reused and never instantiated mid-session. A single `BallController` instance guarantees one ball.
- Debug: `F` key / in-VR debug button launches the deterministic default (80 km/h, middle stump line, length 6 m from stumps). A bounce marker plus a logged bounce point are used for the AC-04 ten-launch check.

## 10. Contact detection and response (M5, T016–T020)
- Layer collision matrix: `Ball` collides with `Pitch`, `Boundary`, `Stumps`, `BatProxy`. `ZoneTrigger` is trigger-only. `BatProxy` doesn't collide with anything else.
- **Two detectors, one authority.**
  - (a) `OnCollisionEnter` on the ball with `BatProxy`.
  - (b) `SweptBatBallTest` runs every `FixedUpdate` *before* the simulation. It interpolates the bat pose (lerp/slerp) between the previous and current proxy poses and the ball's ballistic position over the same interval, substepped. N = ceil(max(bat tip displacement, ball displacement) / (0.5 × r)), capped at 32. It tests sphere-vs-OBB at each substep, and initial overlap counts as a contact at t = 0.
  - Both feed `BatBallContactResolver.TryResolve(deliveryId, contact)`, which accepts **only the first** contact per delivery while not suppressed. Later bat contacts are ignored, and ground/stump/zone events continue.
- **Resolver owns the outgoing velocity.** It runs in the collision callback or pre-step, and sets `rb.linearVelocity` (overwriting any solver bounce), then repositions the ball out of overlap along the normal by r + 1 mm. For one fixed step after acceptance, Ball↔BatProxy collisions are ignored via `Physics.IgnoreCollision`, then restored, so the solver can't add a second bounce.
- Response (`BatContactMath`):
  - `n` = blade face normal at the contact, sign toward the ball. `v_bat = PoseHistory.PointVelocity(contactPoint)`. `v_rel = v_ball − v_bat`, `vn = v_rel·n`. If vn ≥ 0, reject (separating).
  - `e = lerp(eEdge, eSweet, sweetFactor)`. The sweet spot is about 0.15–0.25 m from the blade toe and falls off smoothly. Toward the blade edge in width, e is reduced and n is tilted by a bounded, deterministic angle (no random numbers).
  - `v_out_rel = −e·vn·n + (1 − μt)·v_t`, and `v_out = v_bat + v_out_rel`.
  - Clamp |v_out| ≤ 45 m/s. Assert finite, otherwise fall back to a dead-bat drop and log it.
  - As a result: a stationary face deflects, a soft defence gives low exit speed, a fast aligned swing gives high exit speed, face angle sets direction, and loft sets launch angle.
- Feedback: an impact AudioSource one-shot with volume ∝ clamp(|vn|). Haptics on the bat controller through XRI `HapticImpulsePlayer` (or `SendHapticImpulse`) with amplitude 0.3–0.6 for 0.05–0.08 s.
- Debug markers: contact point, normal, and velocity rays, shown only when `GameConfig.debugContacts` is on and only in development builds.
- Fixed timestep: stay at the default 0.02 s through M4. At M5, measure contact-matrix results at 0.02, 1/72, and 1/90 s, and profile on Quest. Record the chosen value and its reason in plan.md.

## 11. Session flow (M7, T025–T028)
- States: `Ready → Countdown → DeliveryActive → Resolving → BetweenBalls → (Countdown | SessionComplete)`. `SessionComplete → Ready` on Restart. `Paused` can be entered from Countdown, DeliveryActive, Resolving, or BetweenBalls.
- Timers are float fields advanced in `SessionManager.Update`. **No coroutines or Invoke.** Every scheduled action captures `generation`, so Restart and Pause increment `generation` and stale actions no-op. Ball events carry `deliveryId`, and a mismatch is dropped.
- Finalize: the `SessionStateMachine.AddResult(result)` guard rejects a duplicate ID, a wrong ID, or a seventh record. Totals (balls faced, practice runs, contacts, misses, bowled) are computed from the records.
- Interruption: `Application.focusChanged(false)`, `OnApplicationPause(true)`, HMD `isTracked` false, or both-controller loss → Paused. Paused means:
  - the ball is reset and the active delivery is cancelled, not recorded
  - the timers stop
  - bat history is cleared
  
  The UI then shows "Paused — press Ready to retry ball N". Confirming goes to Countdown for the same ball number. There is no auto-resume.
- UI: the Start panel (handedness toggle, alignment nudge, clear-space reminder, Start), the live scoreboard (ball n/6, runs, contacts, misses, bowled, last result + carry), and the Results panel (6 rows + totals, "Practice runs" label, Restart). Use the XRI ray interactor on the non-bat controller for UI.

## 12. Testing (per CLAUDE.md §13)
- **EditMode (Core):**
  - km/h conversion
  - solver reachability and refined bounce error < 1 cm
  - point velocity with pure rotation
  - shortest-path quaternion
  - stationary-face deflection
  - defence slower than attack
  - finite and clamped outputs across random-seeded fuzz
  - scoring priority and half-open bounds
  - unhit zone = 0
  - contact-then-bowled = 0 with both flags
  - carry N/A after a net hit
  - state machine: six-limit, duplicate/stale rejection, restart reset, pause without consumption
- **PlayMode:**
  - 10 identical launches with bounce within 0.25 m (AC-04)
  - no residual state after reset
  - scripted bat sweeps (a `ScriptedBatDriver` replaces the controller source): block, soft defence, drive, loft, off-centre, tip arc at several speeds, initial overlap, 10 runs each, asserting exactly one impulse and no tunneling (AC-05 scripted part)
  - countdown cancel, restart in each phase × 5 (AC-08)
  - simulated focus loss (AC-09 logic)
- Run tests through the Test Runner, or in batch mode with `-runTests -testPlatform EditMode|PlayMode` when the Editor is closed. Results go to `validation.md`.

## 13. Performance (M8)
- Budget: 72 Hz (13.9 ms). Measure with the MQDH Performance HUD / OVR Metrics Tool and the Unity Profiler connected to a development build over USB/ADB. Record CPU/GPU ms separately.
- Expected cost is trivial. Investigate only demonstrated problems: avoid per-frame allocation in PoseHistory and the resolver, keep shared materials, and turn off debug logs in release builds.

## 14. Milestone execution order
Execute strictly M1 → M8 per `tasks.md`. Each milestone ends with:
- a tasks.md tick for the code work
- a `validation.md` evidence entry for each check actually run
- a `Docs/PROGRESS.md` row update
- precise owner headset steps when device validation is pending

The MVP isn't claimed done until AC-10 has real device evidence. **First implementation action: M1 (T001–T006).** The owner installs Unity Hub, the Editor, and the modules listed in §1. The agent then configures the project, scene builder, and build script, produces the APK, and hands over the AC-01 headset checklist.

## Requirement traceability

| Requirement | Plan sections | Tasks | Acceptance |
| --- | --- | --- | --- |
| REQ-001 | §1, §2, §3, §4 | T001–T006 | AC-01 |
| REQ-002 | §5 | T007–T009 | AC-02 |
| REQ-003 | §8 | T010, T012 | AC-03 |
| REQ-004 | §9 | T013–T015 | AC-04 |
| REQ-005 | §9 | T013, T015 | AC-04 |
| REQ-006 | §10 | T016–T017, T020 | AC-05 |
| REQ-007 | §8, §10 | T011, T016, T018, T020 | AC-03, AC-05 |
| REQ-008 | §6, §7, §11 | T021, T024 | AC-06 |
| REQ-009 | §6 | T022, T024 | AC-06 |
| REQ-010 | §6 | T023–T024 | AC-06 |
| REQ-011 | §11 | T025–T026, T028 | AC-07 |
| REQ-012 | §11 | T026, T028 | AC-07 |
| REQ-013 | §11 | T025, T028 | AC-08 |
| REQ-014 | §8, §11 | T027–T028 | AC-09 |
| REQ-015 | §5, §10, §11 | T008, T019, T029 | AC-02, AC-05 |
| REQ-016 | §13, §14 | T030–T032 | AC-10 |

## Open owner decisions

1. **Zone geometry and boundary-exit rule (§6).** The proposal: front-wall zones split by impact height, side/rear walls score 1, a contacted ball clearing the front wall scores 6, a contacted ball clearing a side or rear wall scores 1, an unhit exit scores 0, and the launcher has no collider. This affects REQ-009 and AC-06. It must be confirmed before M6.
2. ~~Unity 6 LTS patch version~~: resolved 2026-10-23, 6000.3.27f1 (see decisions log).
3. **Default delivery length.** The proposal is a bounce 6 m from the stumps at 80 km/h. It gets confirmed after M4 measurement, with slower deliveries used for early M5 batting.

## Decisions log

| Date | Decision | Reason | Affects |
| --- | --- | --- | --- |
| 2026-10-23 | Create plan.md baseline (this document) | Missing from starter package | All |
| 2026-10-23 | Bat impulse owned by `BatBallContactResolver`, not the physics solver | Spec requires one contact authority and bounded, explainable response | REQ-006, REQ-007 |
| 2026-10-23 | Session timers in `Update` with a generation counter, no coroutines | Restart and pause must cancel all pending work deterministically | REQ-013, REQ-014 |
| 2026-10-23 | Keep fixed timestep 0.02 s until measured at M5 | CLAUDE.md: measure before reducing | REQ-007, REQ-016 |
| 2026-10-23 | Unity 6000.3.27f1; packages resolved by Package Manager for that Editor (OpenXR 1.18.0, XRI 3.6.1, XR Management 4.7.0, Input System 1.20.1, URP 17.3.0) | Newest 6.3 LTS patch offered by Hub; resolves open decision 2 | REQ-001 |
| 2026-10-23 | Project created with `-createProject -cloneFromTemplate` (Universal 3D 17.0.14) in a scratch folder, then moved here; template tutorial/sample scene/project-wide input actions removed; unused packages (Visual Scripting, Multiplayer Center, Collab, AI Navigation, Timeline) removed | CLAUDE.md: keep only what the prototype uses | REQ-001 |
| 2026-10-23 | Quest quality level uses `Mobile_RPAsset` changed to HDR off, MSAA 4x, render scale 1.0; SSAO feature removed from the desktop renderer | Plan §3; render scale 0.8 blurs the ball; SSAO triggers an OpenXR performance warning | REQ-001, REQ-015, REQ-016 |
| 2026-10-23 | OpenXR latency mode "Prioritize Input Polling" | Lower controller-pose latency for batting | REQ-003, REQ-007 |
| 2026-10-23 | XRI Starter Assets locomotion providers disabled in the scene, not deleted | Stationary game; the prefab stays unmodified for future XRI updates | REQ-002 |
| 2026-10-23 | M2 geometry constants in `CricketVR.Core/FieldDimensions.cs`; layers Pitch (6), Boundary (7), Stumps (8) | One source of truth for builder, bowling and scoring | REQ-002, REQ-004, REQ-009 |
| 2026-10-23 | Translucent net panels instead of a grid texture; scoreboard 2.4 m away rather than 3 m; stumps single solid box | Quest aliasing; 3 m would sit in the side net; simplest reliable wicket collider | REQ-002, REQ-015 |
| 2026-10-23 | Builder migrates untouched M1 defaults only (floor resize, XR Origin to stance, M1 scale references deactivated as they stand in the lane/swing space) | Preserve owner edits while letting the scene evolve | REQ-002 |
| 2026-10-23 | Owner alignment baked: right hand tilt 25, face 5, lean 0, spin 10, slide 0, face flipped (spin and face flip rotate about the bat's own length, applied last); left hand mirrored guess, unverified | Owner-approved in headset (E-011, E-013) | REQ-003 |
| 2026-10-23 | Realistic procedural bat mesh (blade, grip, splice, sticker, willow grain texture) generated by `BatMeshGenerator`; no downloaded models | Owner request ("looks like a toy"). Explicit owner instruction overrides CLAUDE.md's primitives-first guidance for the bat; no third-party assets per CLAUDE.md §14. Visible face kept on the proxy's face plane | REQ-003, REQ-015 |
| 2026-10-23 | Default bat frame from the owner's reference mount: toe along grip +Z (past the ring), face parallel to the button face (grip +Y); in-headset 3-axis + slide alignment, final values to be baked into GameConfig | Owner rejected the palm-facing frame (E-010) | REQ-003 |
| 2026-10-23 | Bat follows dedicated grip-pose trackers (TrackedPoseDriver on devicePosition/deviceRotation), not the Starter Assets controllers (aim pose); handle along grip +Z, face along the palm; face turn wraps with an in-game 180 degree flip | Aim pose is tilted relative to the handle: caused the reversed (E-008) and then 20-30 degree offset (E-009) bat | REQ-003 |
| 2026-10-23 | Proceed with M2 while the left controller is out of service | Owner request; left controller is a hardware issue (OVRRemoteService: paired, Searching, battery unknown) | REQ-001, REQ-003 |
