# QA report

Build tested: 2026-11-10. Unity 6000.6.6f1, URP.
Devices: **PC**: Linux, Intel Core i5-10300H with Intel UHD (CML GT2), 1854×1011 window.
**Phone**: Samsung Galaxy A22 5G (SM-A226B), Android 13, MediaTek Dimensity 700, Mali-G57 MC2,
3.6 GB RAM, 1080×2408 at 60 Hz.

How each result was obtained:

- **Device**: done on the real PC or phone (built player), by playing or with the in-game benchmark.
- **Auto**: automated Play Mode / Edit Mode tests in the editor (simulated input, GPS and steps).
- **Pending**: not tested yet.

A feature is listed as working only when one of those two columns shows it.

## Automated tests

| Suite | Result |
|---|---|
| Play Mode | 217 / 217 in the last full run (2026-11-10), plus the 12 aspect-ratio cases added afterwards (12 / 12) |
| Edit Mode | 7 / 7 |

## PC

| Area | Device | Auto | Notes |
|---|---|---|---|
| Keyboard movement, sprint, crouch, jump | Benchmark flow | `PlayerControllerTests` | |
| Mouse camera | | `PlayerControllerTests`, `MenuFlowTests` | |
| Interactions (terminals, pickups, doors) | | `NpcAndInteractionTests`, `MissionFlowTests` | |
| Missions (start, objectives, rewards, retry) | Benchmark starts a contract in Gangnam | `MissionFlowTests`, `DestinationTests` | |
| Map (minimap, full map, markers) | | `MapTests` | |
| UI and menus | Benchmark: every menu scene | `MenuFlowTests`, `AspectRatioTests` | |
| Save system | | `ProgressionTests`, `GpsMissionTests`, `ActivitySessionTests` | Versioned (v8), validated, atomic, backup |
| Economy | | `MissionFlowTests`, `ProgressionTests` | Ledger, rewards paid once |
| Performance | **Benchmark, see below** | `QualityTests` | |

## Android

| Area | Device (Galaxy A22) | Auto | Notes |
|---|---|---|---|
| Install, launch, landscape, Hangul text | ✔ | | |
| Menus (main, operative, destinations, contracts, pause, settings) | ✔ played with touch | `MenuFlowTests`, `AspectRatioTests` | |
| Joystick movement | ✔ held finger moved the operative | `TouchControlsTests` | |
| Camera touch controls | ✔ drag rotated the camera | `TouchControlsTests` | |
| Simultaneous touches | **Pending**: adb simulates one finger only | ✔ two and three fingers through the EventSystem | Needs real fingers |
| Jump | ✔ | `TouchControlsTests` | |
| Sprint, crouch toggles | | `TouchControlsTests` | Not tried by hand on the phone |
| Interactions (tap / hold) | | `TouchControlsTests` | |
| Map button, pinch zoom | | `TouchControlsTests` | |
| Save system | ✔ the tester's career and GPS totals survived restarts and reinstalls (`-r`) | | |
| GPS permissions | ✔ explanation screen; states "Not asked" → "Granted"; location off reported | `LocationTests` | Deny path pending |
| Real walking detection, distance tracking | **Pending** (outdoor walk) | ✔ simulated traces | See the [GPS test plan](GPS_TEST_PLAN.md) |
| Step counting | Partly: sensor detected, 21 steps counted in the tester's session | ✔ | Not compared with a counted walk |
| Checkpoints, activity rewards | **Pending** | ✔ | |
| Performance | **Benchmark, see below** | `PhonePresetTests` | |
| Logs | ✔ clean: no warnings or errors except Unity's harmless Play Asset Delivery probe | `DeviceFindingsTests` | |

## Screen sizes and aspect ratios

`AspectRatioTests` lays every screen out at 1920×1080 (16:9), 1920×1200 (16:10), 1600×1200 (4:3),
2408×1080 (20:9), 2560×1080 (21:9) and 1280×720. With touch controls on and off it checks that
every pressable element (buttons, toggles, sliders, touch buttons) is fully on screen, also with
the pause menu and the settings open. Verified to catch the 20:9 bug found on the phone (a
deliberately re-introduced bug made it fail). Real screens checked: 2408×1080 phone, 1854×1011 PC window.

## Performance (in-game benchmark, Gangnam and Myeongdong tours)

**Galaxy A22 5G** (Android presets, 60 Hz display, 0 frames over 50 ms on Low):

| Preset | Gangnam | Myeongdong | Menus |
|---|---|---|---|
| Low (recommended for this phone) | 46–47 fps (median 60) | 60 fps | 30 fps (battery cap) |
| Medium | 22 fps | 25 fps | 22–30 fps |
| High | 14 fps | 17 fps | 16 fps |

Memory: ~255 MB allocated, no garbage collections during play. Battery 34–35 °C, thermal status 0
during the runs (each run ~2 minutes; a long session is still pending).

**PC** (Intel UHD, 1854×1011, V-Sync off):

| Preset | Gangnam | Myeongdong | Stage 10 (Gangnam, before the Android work) |
|---|---|---|---|
| Low | 63 fps | 77 fps | 60 fps |
| Medium | 37 fps | 45 fps | 38 fps |
| High | 22 fps | 26 fps | 22 fps |

The Android work did not slow down the PC version.

## Bugs found on the real phone (all fixed and re-checked on the phone)

| Bug | Cause | Fix |
|---|---|---|
| No post-processing in the APK | URP stripped the unused depth-of-field shader, which disabled every post-processing pass | Keep post-processing resources in builds |
| "Renderer registered with more than one LODGroup" | Sign text inside props had its own LOD group | One LOD group per prop |
| Contract board's Accept button cut off on 20:9 | 1920×1080 layouts scaled halfway between width and height | "Expand" matching on all UI canvases |
| Keyboard hints and "[ESC]" on the phone; preview could not be rotated by touch | PC-only hints; mouse-only rotation | Touch-aware hints, touch rotation |
| Back from GPS setup (opened in Settings) opened the dashboard | Fixed return screen | Opens over Settings and returns there |
| "The referenced script is missing" on `[MapManager]` | Component of a deleted early-stage script | Removed by the map builder |
| "Failed to create agent: no valid NavMesh"; frozen guards | NPC agents enabled before the scene's NavMesh registered | Agent enabled in `Start`, plus a retry |
| Medium chosen automatically, 22 fps | GPU rule ignored the Mali core count | Mali with 2–3 cores → Low |
| Benchmark unusable on Android | Desktop command line, preset overridden, wrong screenshot path | Intent arguments, settings-based preset, relative path |

## Known limitations and open issues

- **GPS outdoor walk not done**: distance accuracy, checkpoints, rewards and pause/resume are
  verified only with simulated GPS. Battery drain during a walk is unmeasured.
- **Multi-touch on the real screen** is untested (automated tests only).
- **Heat over a long session** (15–20 min) is unmeasured.
- **One phone only.** The automatic preset choice is a rule of thumb for other GPUs.
- **Intermittent**: one PC benchmark run (Medium) waited more than 15 s for Mission Selection to
  load; the other five PC and phone runs did not. Not reproduced yet.
- Two districts playable (Gangnam, Myeongdong); the other destinations are "in development".
- Buildings are generated from simple shapes with CC0 textures; characters are procedural
  placeholders (no rigged models or 3D packs imported).
- The APK is signed with Unity's debug key: a public release needs a project keystore.
- Windows build not produced (module not installed on this machine).
- The manifest includes `INTERNET` (added by Unity by default); the game sends no data.

## Manual Unity Editor tasks

- Rigged characters: assign a prefab to `CharacterData.characterPreviewPrefab` and an Animator
  Controller on `PlayerCharacterApplier`.
- Release signing: Player Settings → Android → Publishing Settings → custom keystore.
- Windows: install *Windows Build Support* in Unity Hub.
- After editing a builder: `CONTRACT > Rebuild Everything`, then run the tests.
