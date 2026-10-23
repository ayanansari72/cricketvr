# Validation and evidence

## Current truth

As of 2026-10-23: the Unity project exists, its Editor scripts compile, OpenXR project validation reports 0 errors and 0 warnings for Android, and a development APK builds. **Nothing has been installed on or tested with the Quest yet.** Game implementation (M2+), headset testing, and performance profiling are not yet performed.

Documentation checks confirm that requirements map to tasks and scenarios. That is not game validation.

## Acceptance register

| Scenario | Requirements | Status | Evidence/build | Next action |
| --- | --- | --- | --- | --- |
| AC-01 | REQ-001 | Partial: 4/5 owner checks pass; left controller not working (cause not isolated) | APK sha256 2076861…a47fe; E-001–E-004 | Owner: test left controller in Quest Home, then in CricketVR |
| AC-02 | REQ-002, REQ-015 | **Pass** | APK sha256 f35ebef…f8192; E-005, E-006 | Re-check if the stance, net or scoreboard moves (M3/M7) |
| AC-03 | REQ-003, REQ-007 | Not run | None | M3 hand/alignment/tracking test |
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
