# CricketVR

A small standalone VR cricket batting prototype for Meta Quest 3: stand at the crease, face six deliveries, see the results, play again. Built on a Mac, run on the headset.

Status: **M4 (bowling machine) complete and checked on the headset; M5 (bat–ball contact) next.** M1 passed on the headset except the left controller, which is out of service (hardware). See `Docs/PROGRESS.md`.

## Versions (recorded 2026-10-23)

| Component | Version |
| --- | --- |
| Unity Editor | 6000.3.27f1 (Unity 6.3 LTS, Apple Silicon) |
| Unity Hub | 3.22.4 (installed with `brew install --cask unity-hub`) |
| Android SDK/NDK/OpenJDK | Hub-managed modules of the Editor; NDK 27.2.12479018, OpenJDK 17.0.18 |
| Universal RP | 17.3.0 |
| Input System | 1.20.1 |
| XR Plug-in Management | 4.7.0 |
| OpenXR Plugin | 1.18.0 (Meta Quest Support, Oculus Touch + Meta Quest Touch Plus profiles) |
| XR Interaction Toolkit | 3.6.1 (+ Starter Assets sample) |
| XR Core Utils | 2.6.0 |
| Test Framework | 1.6.0 |

Exact package resolution is in `Packages/packages-lock.json`. TextMeshPro essential resources are committed under `Assets/TextMesh Pro` (imported with `Unity -batchmode -quit -projectPath . -importPackage "<ugui package>/Package Resources/TMP Essential Resources.unitypackage"`). Physics runs at 144 Hz (Fixed Timestep 1/144 s). Don't upgrade packages mid-milestone.

## Build settings (applied by the setup menu)

Android, IL2CPP, ARM64 only, min API 32, Vulkan, Linear color, ASTC. OpenXR loader on Android with Single Pass Instanced rendering and "Prioritize Input Polling" latency mode. The Quest uses the `Mobile` quality level / `Assets/Settings/Mobile_RPAsset` (HDR off, 4x MSAA, render scale 1, no post-processing features). Application ID `com.cricketvr.prototype`.

## Reopening the project

1. Open **Unity Hub**, sign in, and confirm **Settings → Licenses** shows an active Personal license.
2. **Projects → Add → Add project from disk**, choose this folder (`cricketvr`), and open it with 6000.3.27f1.

## Setup menu (idempotent, safe to re-run)

The **CricketVR** menu in the Editor menu bar:

| Menu item | What it does |
| --- | --- |
| Setup → 1. Install Required Packages | Adds Input System, XR Management, OpenXR, XRI, Test Framework |
| Setup → 2. Import XRI Starter Assets | Imports the XRI sample that provides the XR Origin rig |
| Setup → 3. Apply Quest Player Settings | Android target and player settings listed above |
| Setup → 4. Configure OpenXR for Quest | Loader, Meta Quest feature, controller profiles; applies automatic validation fixes |
| Setup → 5. Report OpenXR Validation Issues | Logs remaining validation errors/warnings |
| Setup → 6. Build or Repair Main Scene | Adds any missing objects to `Assets/CricketVR/Scenes/Main.unity`; never removes or moves existing ones |
| Build → Quest APK (Development) | Writes `Builds/CricketVR.apk` |
| Build → Quest APK (Development) and Run on Headset | Builds, installs, and launches on a connected Quest |

The same steps run from Terminal when the Editor is **closed** (never run batch mode against a project open in the Editor):

```sh
UNITY=/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -quit -projectPath . -executeMethod CricketVR.Editor.XrProjectConfigurator.ConfigureBatch -logFile Logs/configure.log
"$UNITY" -batchmode -quit -projectPath . -executeMethod CricketVR.Editor.MainSceneBuilder.BuildOrRepairBatch -logFile Logs/scene.log
"$UNITY" -batchmode -projectPath . -executeMethod CricketVR.Editor.QuestBuild.Build -logFile Logs/build.log
```

`QuestBuild.Build` exits with code 0 on success and non-zero on failure.

## Installing on the Quest 3

One-time owner setup:

1. Create or join a Meta developer organization at <https://developers.meta.com/horizon/manage/> (required for Developer Mode).
2. In the **Meta Horizon** phone app: **Menu → Devices → (your Quest) → Headset settings → Developer Mode → On**. Restart the headset.
3. Connect the Quest to the Mac with a USB-C cable that carries data. In the headset, accept **Allow USB debugging** (tick "Always allow from this computer").

Install and run:

```sh
ADB=/Applications/Unity/Hub/Editor/6000.3.27f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
"$ADB" devices -l                          # the Quest should be listed as "device", not "unauthorized"
"$ADB" install -r Builds/CricketVR.apk
"$ADB" shell monkey -p com.cricketvr.prototype -c com.oculus.intent.category.VR 1
"$ADB" logcat -s Unity                    # optional: live Unity logs
```

Afterwards the app is under **App Library → Unknown Sources** (or **Library → filter "Unknown Sources"**) and runs without the cable. Meta Quest Developer Hub (MQDH) can do the same install by drag-and-drop if you prefer a GUI.

Meta Horizon Link's direct Editor preview is Windows-only; it isn't part of this workflow.

## Repository layout

```text
Assets/CricketVR/        Project code, scenes, materials (Scripts/Editor = setup and build tooling)
Assets/Samples/          XRI Starter Assets (XR Origin rig, input actions, controller visuals)
Assets/Settings/         URP assets from the Universal 3D template
Assets/XR/               XR Management and OpenXR settings
Docs/PROGRESS.md         Milestone status
specs/001-six-ball-net/  spec, plan, tasks, validation evidence
```

## Spec-driven workflow

| File | Purpose |
| --- | --- |
| `CLAUDE.md` | Agent workflow, project conventions, and background |
| `specs/001-six-ball-net/spec.md` | Required behavior and acceptance scenarios |
| `specs/001-six-ball-net/plan.md` | Technical approach and decisions |
| `specs/001-six-ball-net/tasks.md` | Ordered, traceable implementation work |
| `specs/001-six-ball-net/validation.md` | Evidence and remaining device checks |

For each milestone: read its requirements, refine the plan if needed, complete the mapped tasks, run checks, and record evidence. Headset checks stay pending until actually performed.

## Known limitations

- The scene has the practice net, pitch, creases, stumps, launcher, a tracked bat, and a debug bowling machine (tap A = bowl again, tap B = next preset; F / N keys in the Editor). Bat-ball contact is still the physics engine's default and can miss (M5). No scoring or session yet.
- Bat setup controls (until the M7 menu): hold A = switch handedness; hold B = move the bat to the other controller; thumbstick up/down = tilt, left/right = spin; trigger + left/right = lean; grip + up/down = slide; grip + left/right = face turn; tap stick click = reset, hold = swap face and back.
- Where you stand in the game is the centre of your Quest boundary. Stand there and hold the Meta button to reset the view while facing the launcher.
- The XRI Starter Assets rig still carries ray/grab interactors; they are harmless now and will be trimmed when bat and UI work begins (M3/M7).
