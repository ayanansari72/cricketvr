# Validation and evidence

## Current truth

As of 2026-10-23: the Unity project exists, its Editor scripts compile, OpenXR project validation reports 0 errors and 0 warnings for Android, and a development APK builds. **Nothing has been installed on or tested with the Quest yet.** Game implementation (M2+), headset testing, and performance profiling are not yet performed.

Documentation checks confirm that requirements map to tasks and scenarios. That is not game validation.

## Acceptance register

| Scenario | Requirements | Status | Evidence/build | Next action |
| --- | --- | --- | --- | --- |
| AC-01 | REQ-001 | Partial: 4/5 owner checks pass; left controller not working (cause not isolated) | APK sha256 2076861…a47fe; E-001–E-004 | Owner: test left controller in Quest Home, then in CricketVR |
| AC-02 | REQ-002, REQ-015 | **Pass** | APK sha256 f35ebef…f8192; E-005, E-006 | Re-check if the stance, net or scoreboard moves (M3/M7) |
| AC-03 | REQ-003, REQ-007 | **Pass (right controller)**; left-hand bat pending hardware | APK sha256 9a0e7fc…2663b; E-007–E-014 | Re-run left-hand checks when the left controller works |
| AC-04 | REQ-004, REQ-005 | Not run | None | M4 repeatable launch test |
| AC-05 | REQ-006, REQ-007, REQ-015 | Not run | None | M5 scripted and device contacts |
| AC-06 | REQ-008, REQ-009, REQ-010 | Not run | None | M6 outcomes/metrics |
| AC-07 | REQ-011, REQ-012 | Not run | None | M7 six-delivery loop |
| AC-08 | REQ-013 | Not run | None | M7 restart checks |
| AC-09 | REQ-014 | Not run | None | M7 interruption checks |
| AC-10 | REQ-016 | Not run | None | M8 regression/profile |

## Evidence entry template

Copy for each executed check:

```text
Check ID / acceptance scenario:
Requirement IDs:
Date and tester:
Git commit or build identifier:
Unity and package versions:
Environment: static review / EditMode / PlayMode / APK build / Quest device
Configuration (speed, pose, seed, handedness, refresh rate as applicable):
Steps or exact command:
Expected result:
Actual result and measured values:
Outcome: Pass / Fail / Blocked / Partial
Log or artifact path:
Remaining limitation / follow-up task:
```

## Evidence log

```text
Check ID / acceptance scenario: E-001 / T001–T004 setup (supports AC-01)
Requirement IDs: REQ-001
Date and tester: 2026-10-23, agent
Git commit or build identifier: uncommitted working tree (first commit pending owner)
Unity and package versions: Unity 6000.3.27f1; URP 17.3.0; Input System 1.20.1; XR Management 4.7.0; OpenXR 1.18.0; XRI 3.6.1; XR Core Utils 2.6.0
Environment: Editor batch mode (compilation + project validation)
Configuration: Android, IL2CPP, ARM64, min API 32, Vulkan, Linear, ASTC; OpenXR + Meta Quest Support + Oculus Touch + Meta Quest Touch Plus profiles; Single Pass Instanced; Prioritize Input Polling
Steps or exact command: Unity -batchmode -projectPath . -executeMethod CricketVR.Editor.XrProjectConfigurator.ConfigureBatch, then MainSceneBuilder.BuildOrRepairBatch (run twice)
Expected result: no compile errors; 0 OpenXR validation errors; scene contains exactly one XR Origin; second builder run adds nothing
Actual result and measured values: no compile errors; "OpenXR validation: 0 error(s), 0 warning(s)"; scene created with Floor, Cube_1m, Post_1.8m, one XR Origin (XR Rig) with floor tracking origin and all 10 locomotion providers disabled; second run "Main scene already complete; nothing added."
Outcome: Pass (static/Editor checks only)
Log or artifact path: batch logs in the agent session scratchpad (not kept in repo)
Remaining limitation / follow-up task: Needs on-device AC-01. One earlier failure fixed: the OpenXR 1.18.0 Meta Quest validation rule threw a NullReferenceException in batch mode because selectedBuildTargetGroup stayed Standalone; the bootstrap now sets it to Android.
```

```text
Check ID / acceptance scenario: E-002 / T005 APK build (supports AC-01)
Requirement IDs: REQ-001
Date and tester: 2026-10-23, agent
Git commit or build identifier: Builds/CricketVR.apk, 56,307,154 bytes, sha256 207686185d625c9b30efee767300da6de73ae67684273b766328fd8f3f9a47fe
Unity and package versions: as E-001
Environment: APK build (batch mode)
Configuration: Development build, scene Assets/CricketVR/Scenes/Main.unity
Steps or exact command: Unity -batchmode -projectPath . -executeMethod CricketVR.Editor.QuestBuild.Build
Expected result: build succeeds; APK is arm64-only with Quest VR manifest entries
Actual result and measured values: "Build Succeeded ... 0 error(s), 0 warning(s)"; aapt2: package com.cricketvr.prototype, native-code arm64-v8a, targetSdk 36, categories LAUNCHER + com.oculus.intent.category.VR, android.hardware.vr.headtracking, com.oculus.supportedDevices, com.oculus.vr.focusaware
Outcome: Pass (build only; not proof of headset behavior)
Log or artifact path: Builds/CricketVR.apk (gitignored)
Remaining limitation / follow-up task: `adb devices` listed no device on 2026-10-23; install + AC-01 pending.
```

```text
Check ID / acceptance scenario: E-003 / T005 install + launch (partial AC-01, log evidence only)
Requirement IDs: REQ-001
Date and tester: 2026-10-23, agent (owner connected the headset)
Git commit or build identifier: Builds/CricketVR.apk sha256 207686185d625c9b30efee767300da6de73ae67684273b766328fd8f3f9a47fe
Unity and package versions: as E-001
Environment: Quest device (Quest 3 "eureka", Android 14 / API 34, build UP1A.231005.007.A1), USB/ADB
Configuration: development build, default 72 Hz
Steps or exact command: adb install -r Builds/CricketVR.apk; adb shell monkey -p com.cricketvr.prototype -c com.oculus.intent.category.VR 1; adb logcat -d --pid=<pid>
Expected result: installs; OpenXR session reaches FOCUSED; controller profiles load; no app errors
Actual result and measured values: "Success" install; session IDLE->READY->SYNCHRONIZED->VISIBLE->FOCUSED; profiles loaded: Meta Quest Touch Plus, Oculus Touch; only error lines are OpenXR-Loader "Could access neither the installable nor system runtime broker", followed by a successful fallback to /odm/etc/openxr/1/active_runtime.aarch64.json. VrApi: FPS=72/72, Tear=0, Stale=0, App=1.98 ms, CPU&GPU=5.04 ms (idle placeholder scene; not an AC-10 performance result)
Outcome: Partial. App launch and session are confirmed from logs; visual stereo, head tracking, controller tracking, floor scale, and untethered launch still need owner observation
Log or artifact path: device logcat (not stored)
Remaining limitation / follow-up task: owner headset checklist for AC-01
```

```text
Check ID / acceptance scenario: E-004 / AC-01 owner headset observations
Requirement IDs: REQ-001
Date and tester: 2026-10-23, owner
Git commit or build identifier: Builds/CricketVR.apk sha256 207686185d625c9b30efee767300da6de73ae67684273b766328fd8f3f9a47fe
Unity and package versions: as E-001
Environment: Quest device, manual observation
Configuration: default 72 Hz; tethered, then untethered relaunch from App Library > Unknown Sources
Steps or exact command: owner checklist: (1) stereo/head tracking, (2) floor height and scale references, (3) both controllers tracked, (4) no thumbstick locomotion, (5) untethered relaunch
Expected result: all five pass
Actual result and measured values: owner reported (1) pass, (2) pass, (4) pass, (5) pass. (3) right controller tracked fine; left controller "not working right now". Cause not yet isolated: controller hardware/battery vs app.
Outcome: Partial
Log or artifact path: owner report in session
Remaining limitation / follow-up task: owner to check the left controller in Quest Home. If it fails there too (hardware), fix the battery/pairing and re-check in CricketVR. If it works in Home but not in the app, capture logcat and debug.
```

```text
Check ID / acceptance scenario: E-005 / T007–T008 practice net (supports AC-02)
Requirement IDs: REQ-002, REQ-015
Date and tester: 2026-10-23, agent
Git commit or build identifier: Builds/CricketVR.apk sha256 f35ebefbeedb6281cc65c24b5197b2dc4d70bf4dfa3ea742ee2441bba24f8192 (uncommitted tree)
Unity and package versions: as E-001
Environment: Editor batch mode (scene builder), EditMode tests, APK build, install on Quest
Configuration: right-handed stance (-0.3, 0, 1.0); release point (0, 2.0, 18.9)
Steps or exact command: MainSceneBuilder.BuildOrRepairBatch (twice); Unity -batchmode -runTests -testPlatform EditMode; QuestBuild.Build; adb install -r
Expected result: builder idempotent; layout tests pass; build clean; installs
Actual result and measured values: first run added layers Pitch/Boundary/Stumps, pitch, creases, stumps, launcher, net colliders and visuals, stance markers, scoreboard placeholder, and moved the XR Origin to the stance; second run "nothing added". EditMode 11/11 passed: one XR Origin; floor tracking + stance pose; stump collider 0.711 x 0.2286 m on the ground at the origin; stumps 20.12 m apart; pitch top 0-5 mm; launcher has no colliders; net walls on the documented planes; sight line clear at eye heights 1.4/1.6/1.9 m; scoreboard 1.5-3.5 m away and >30 degrees off the delivery line. Build: 0 errors, 0 warnings, OpenXR validation 0/0. adb install Success. Launch not observed: headset asleep (not worn).
Outcome: Pass for scripted/build checks; headset AC-02 pending
Log or artifact path: batch logs and editmode.xml in the agent session scratchpad
Remaining limitation / follow-up task: owner AC-02 check; confirm how Quest recentering interacts with the Floor tracking origin
```

```text
Check ID / acceptance scenario: E-006 / AC-02 owner headset observations
Requirement IDs: REQ-002, REQ-015
Date and tester: 2026-10-23, owner
Git commit or build identifier: Builds/CricketVR.apk sha256 f35ebefbeedb6281cc65c24b5197b2dc4d70bf4dfa3ea742ee2441bba24f8192
Unity and package versions: as E-001
Environment: Quest device, manual observation, right controller only
Configuration: right-handed stance; view reset (hold Meta button) while facing the launcher at the centre of the play area
Steps or exact command: owner checklist (a) on the stance marker behind the crease, stumps behind/right; (b) stumps knee height, pitch and net proportionate; (c) launcher clearly visible, nothing blocking; (d) scoreboard up-right, readable, off the delivery line; (e) natural head motion, nothing moves on its own
Expected result: all pass
Actual result and measured values: owner reported a-e all pass
Outcome: Pass
Log or artifact path: owner report in session
Remaining limitation / follow-up task: left-handed stance not yet exercised (M3); scoreboard text readability is checked in M7
```

```text
Check ID / acceptance scenario: E-007 / T010–T011 tracked bat (supports AC-03)
Requirement IDs: REQ-003, REQ-007
Date and tester: 2026-10-23, agent
Git commit or build identifier: Builds/CricketVR.apk sha256 247cc26576ee54093ae0aa35c6b7511e93d424f770110bad77bef8f469bdda13 (uncommitted on top of 3ab266e)
Unity and package versions: as E-001
Environment: Editor batch mode, EditMode + PlayMode tests, APK build, install on Quest
Configuration: GameConfig defaults: grip offsets 0; limits gap 0.1 s, grip 25 m/s, 60 rad/s, suppress 0.1 s, 3 samples, velocity window 0.03 s
Steps or exact command: MainSceneBuilder.BuildOrRepairBatch (twice); -runTests EditMode; -runTests PlayMode; QuestBuild.Build; adb install -r
Expected result: builder idempotent; tests pass; build clean
Actual result and measured values: builder added BatProxy layer (9), GameConfig asset, Bat (BatController, HandednessSetup, BatSetupControls), BatVisual, kinematic BatPhysicsProxy, and deactivated both Teleport Interactors; second run "nothing added". EditMode 27/27 passed (adds pose history velocity with uneven intervals, rad/s angular velocity, shortest path across quaternion sign flip, 0.05 degree precision, point velocity w x r, recentre jump resets with no velocity across it, rotation jump, time gap, tracking loss/recovery, fast plausible swing accepted (12 m/s, 25 rad/s), NaN rejected, bat frame mirroring). PlayMode 3/3 passed at 72 Hz: proxy follows grip within 2 mm / 0.5 degrees via MovePosition; toe speed = w*r within 10%; 2 m teleport resets and suppresses contacts. A first PlayMode run failed 3/3 ("Rotation quaternions must be unit length"): FixedUpdate ran before any pose was sampled; fixed by gating the proxy on a sampled pose. Build: 0 errors, 0 warnings, OpenXR validation 0/0. Install Success.
Outcome: Pass for scripted/build checks; headset AC-03 pending
Log or artifact path: EditMode.xml / PlayMode.xml in the agent session scratchpad
Remaining limitation / follow-up task: AC-03 on Quest; left-handed bat hand needs the left controller (hardware fault)
```

```text
Check ID / acceptance scenario: E-008 / AC-03 first headset attempt (build 247cc26…)
Requirement IDs: REQ-003, REQ-007
Date and tester: 2026-10-23, owner + agent (device log)
Git commit or build identifier: Builds/CricketVR.apk sha256 247cc26576ee54093ae0aa35c6b7511e93d424f770110bad77bef8f469bdda13
Unity and package versions: as E-001
Environment: Quest device, right controller; adb logcat
Configuration: right-handed, default grip
Steps or exact command: owner held the bat, nudged alignment, swung, reset view; agent read the Unity log
Expected result: bat hangs naturally in a bat-handle grip; recentre resets history; no false fast swings
Actual result and measured values: FAIL on grip: the bat was 180 degrees reversed (blade up in a normal grip; owner had to hold the controller upside down). Owner hit the +/-45 degree tilt clamp trying to correct it. Log: 11 OriginChanged resets on Meta-button recentres, 7 FocusChanged, 20 TrackingLost; 26 swing summaries with peak toe 3.0-8.9 m/s and no ImplausibleMotion resets; no swing logged immediately after a recentre.
Outcome: Fail (grip orientation); recentre gating behaved as designed
Log or artifact path: device logcat summary in session
Remaining limitation / follow-up task: Fixed in build c6e08b439bf25eb9332a7ebf1d7bcf1319d1a0f2110d5ef423243467e9f85fa5: blade along controller +Z; face turn unclamped with a 180 degree flip (hold stick click); saved alignments moved to a v2 key. EditMode 27/27, PlayMode 3/3, build clean. Re-check AC-03, including which side the face points.
```

```text
Check ID / acceptance scenario: E-009 / AC-03 second attempt (build c6e08b4…) and root cause
Requirement IDs: REQ-003
Date and tester: 2026-10-23, owner + agent
Git commit or build identifier: Builds/CricketVR.apk sha256 c6e08b439bf25eb9332a7ebf1d7bcf1319d1a0f2110d5ef423243467e9f85fa5
Unity and package versions: as E-001
Environment: Quest device, right controller; inspection of XRI Default Input Actions
Configuration: right-handed, default grip
Steps or exact command: owner held the bat in a normal grip
Expected result: bat in line with the controller handle
Actual result and measured values: FAIL: bat at roughly 20-30 degrees to the handle (owner estimate); owner nudges only reached tilt -8..0, face 4..8. Root cause: the Starter Assets controller transforms follow pointerPosition/pointerRotation (the aim pose) first, which is tilted relative to the handle. This also explains the earlier 180 degree reversal (E-008).
Outcome: Fail
Log or artifact path: XRI Default Input Actions.inputactions (XRI Right/Left Position/Rotation bindings)
Remaining limitation / follow-up task: Fixed in build bfc96e2184897a8e4da404842dfeec69e78b2f81a2cfdbd24452bf54c7d9b071: new Left Grip / Right Grip objects with TrackedPoseDriver on devicePosition/deviceRotation (grip pose, UpdateAndBeforeRender); bat bound to them; bat frame back to handle along grip +Z; saved alignments moved to a v3 key. Regression test BatFollowsGripPoseTrackersNotAimPose added. EditMode 28/28, PlayMode 3/3, build clean. Re-check AC-03.
```

```text
Check ID / acceptance scenario: E-010 / AC-03 third attempt (build bfc96e2…, grip pose)
Requirement IDs: REQ-003
Date and tester: 2026-10-23, owner
Git commit or build identifier: Builds/CricketVR.apk sha256 bfc96e2184897a8e4da404842dfeec69e78b2f81a2cfdbd24452bf54c7d9b071
Unity and package versions: as E-001
Environment: Quest device, right controller
Configuration: grip pose; handle along grip +Z, face along palm
Steps or exact command: owner held the bat in a normal grip
Expected result: bat orientation matches the owner's intent
Actual result and measured values: FAIL: "completely off again". Owner supplied a reference photo (a commercial VR bat mount): the bat runs along the controller handle, the blade extends past the ring end, and the blade is parallel to the controller's button face. Owner asked for joystick control of the orientation.
Outcome: Fail
Log or artifact path: owner photo in session
Remaining limitation / follow-up task: Build 45addb12d87462439b03020b7469b2173ede3c0c85039e50d5b72d281960e53e: default bat frame from the reference (toe along grip +Z, face normal along grip +Y, blade starts 9 cm past the grip); three rotation axes (tilt, face turn, lean) plus slide, 5 degree / 1 cm steps with repeat; saved alignments v4. EditMode 29/29, PlayMode 3/3. Owner to align and report; agent reads the final values from the device log and bakes them into GameConfig.
```

```text
Check ID / acceptance scenario: E-011 / AC-03 owner alignment (build 45addb1…)
Requirement IDs: REQ-003
Date and tester: 2026-10-23, owner + agent (device log)
Git commit or build identifier: Builds/CricketVR.apk sha256 45addb12d87462439b03020b7469b2173ede3c0c85039e50d5b72d281960e53e
Unity and package versions: as E-001
Environment: Quest device, right controller
Configuration: reference-mount default frame
Steps or exact command: owner aligned with the thumbstick controls; agent read the last logged values
Expected result: owner-approved orientation
Actual result and measured values: final right-hand values tilt 35, face 5, lean 0 degrees, slide 0 cm. Owner: "the last configuration is perfect, just flip the bat" and confirmed (multiple choice) this means swapping face and back, not reversing end-to-end.
Outcome: Pass for orientation (right hand) after flip
Log or artifact path: device logcat in session
Remaining limitation / follow-up task: Build 18bec10bf28ce636008cbc87dacbef0dd788f5e627e5a1dd213c47fb381b7e78 bakes right hand tilt 35 / face 5 / lean 0 / slide 0 / faceFlipped into GameConfig. The face flip is now a 180 degree spin about the bat's own length applied last; the previous in-game flip added 180 to the face turn, which would have mirrored the tilt. Left hand is set to a mirrored guess (tilt 35, face -5, flipped), unverified until the left controller works. Saved alignments moved to v5. EditMode 30/30, PlayMode 3/3, build clean.
```

```text
Check ID / acceptance scenario: E-012 / T012 spin control (owner request)
Requirement IDs: REQ-003
Date and tester: 2026-10-23, agent
Git commit or build identifier: Builds/CricketVR.apk sha256 1c4d2ceee2525d22bafdf9e2299be97dbc62936ef874eaa4d1a9f679b41900e6
Unity and package versions: as E-001
Environment: EditMode + PlayMode tests, APK build, install
Configuration: baked right-hand alignment (tilt 35, face 5, flipped), spin 0
Steps or exact command: -runTests EditMode/PlayMode; QuestBuild.Build; adb install -r
Expected result: thumbstick left/right twists the bat about its own visible handle without changing its angle
Actual result and measured values: owner asked for clockwise/anticlockwise rotation and chose "spin around handle". The existing face turn rotated about the pre-tilt axis, so with 35 degrees of tilt it wobbled instead of twisting. Added `spin`, applied last about the bat's own length; thumbstick left/right now spins; face turn moved to grip + left/right. Test SpinTwistsAboutTheTiltedHandleWithoutChangingTheBatAngle added. EditMode 31/31, PlayMode 3/3, build clean, install Success.
Outcome: Pass (scripted/build); headset feel pending
Log or artifact path: EditMode.xml / PlayMode.xml in the agent session scratchpad
Remaining limitation / follow-up task: owner to try spin in the headset; bake any final spin value
```

```text
Check ID / acceptance scenario: E-013 / final owner alignment + realistic bat (owner request)
Requirement IDs: REQ-003, REQ-015
Date and tester: 2026-10-23, owner + agent
Git commit or build identifier: Builds/CricketVR.apk sha256 9a0e7fcf50cf543f0a490968e1cc92d0a4e876bd6981b40c2d913d95c762663b
Unity and package versions: as E-001
Environment: device log (alignment), Editor batch mode (mesh generation, preview renders), EditMode/PlayMode tests, APK build, install
Configuration: baked right hand tilt 25, face 5, lean 0, spin 10, slide 0, face flipped (owner: "now the bat position is perfect"); left hand mirrored guess (tilt 25, face -5, spin -10, flipped), unverified
Steps or exact command: read the last alignment log line; MainSceneBuilder (generates Models/Bat*.asset and WillowGrain.png); BatPreviewRenderer.Render; -runTests; QuestBuild.Build; adb install -r
Expected result: alignment baked; realistic bat with the visible face on the proxy face; tests and build pass
Actual result and measured values: procedural bat: flat face, curved back with spine (peak 62 mm, 0.20 m above the toe), edges 38 mm, sloped shoulders, rounded toe, willow grain texture (mipmapped), ribbed oval grip, V splice on both sides, unbranded sticker band. Preview renders (face/back/side) checked by the agent. Mesh normal check: all 1083 face vertices +Z. Found and fixed: accent stripe mesh had a single vertex row (no triangles); the preview's first render used stale light (a preview-only artifact). New test VisibleBladeFaceMatchesPhysicsProxyFace (face plane at z = +0.02, width 0.108 m, toe, depth 55-67 mm). Builder second run "nothing added". EditMode 32/32, PlayMode 3/3, build clean, install Success.
Outcome: Pass (scripted/build/preview); owner visual check pending
Log or artifact path: Builds/Preview/Bat_{Face,Back,Side}.png (gitignored)
Remaining limitation / follow-up task: the back of the visual blade is up to 42 mm behind the face while the proxy box is 40 mm thick (20 mm behind the face): back-of-bat contacts differ slightly; M5 decides whether the collider follows the spine.
```

```text
Check ID / acceptance scenario: E-014 / AC-03 owner headset check (realistic bat build)
Requirement IDs: REQ-003, REQ-007, REQ-015
Date and tester: 2026-10-23, owner + agent (device log)
Git commit or build identifier: Builds/CricketVR.apk sha256 9a0e7fcf50cf543f0a490968e1cc92d0a4e876bd6981b40c2d913d95c762663b
Unity and package versions: as E-001
Environment: Quest device, right controller
Configuration: baked alignment (tilt 25, face 5, spin 10, flipped)
Steps or exact command: owner: bat look and position; defensive pushes and 5 full swings; Meta-button recentre (red flash); hold A to switch handedness and back
Expected result: all pass
Actual result and measured values: owner reported all pass. Device log (last ~40 s; 256 KiB buffer had rolled over): 3 OriginChanged resets, Handedness Left then Right with HandChanged resets, TrackingLost resets around recentres. Swing summaries for this build were not captured (rolled off the buffer); swing measurement evidence comes from E-008 (26 swings, peak toe 3.0-8.9 m/s, no false fast swings) and PlayMode tests.
Outcome: Pass for right controller. Left-handed bat hand not exercised (left controller hardware fault).
Log or artifact path: device logcat in session
Remaining limitation / follow-up task: left controller: bat alignment (mirrored guess), handedness with the left bat hand. Capture swing logs to a file on the Mac for future device checks.
```

## Rules

- Name the tested build. Re-test impacted scenarios after material changes; old evidence remains historical.
- Separate compilation, scripted simulation, and manual headset observations.
- If the agent cannot operate the headset, request a precise owner check and leave the result pending until reported.
- For AC-04/05, include per-case counts and failure counts, not only 'looks good'.
- For AC-10, include measured CPU/GPU frame times, refresh target, normal-play interval, observed over-budget behavior, and collection method.
- Capture failures and known limitations. Do not substitute an invented measurement or modify acceptance silently.
- Spec changes should list affected requirements/tasks and invalidate relevant prior evidence explicitly.

## Known open items

- Initial read-only environment inspection on 2026-10-23, before tooling was installed (static check, not validation):
  - macOS 26.5.2 on Apple Silicon, ~150 GB free.
  - Unity Hub, Unity Editor, Android SDK/NDK, and `adb` are not installed.
  - System `git` 2.50.1 is present, but the project folder is not a Git repository.
  - Later the same day: Unity Hub 3.22.4 and Editor 6000.3.27f1 (Android SDK/NDK/OpenJDK) were installed, Git was initialized, and the project was created (E-001). Meta Developer Mode and USB debugging remain owner steps.
- Owner decision PC-01 (zone geometry and boundary exit; see `spec.md` and `plan.md` §6) is pending.
- Grip offsets, delivery launch pose/target, zone geometry, and contact tuning need implementation-time selection and measurement.
- Real headset tracking, bat feel, and frame timing require on-device evidence.
