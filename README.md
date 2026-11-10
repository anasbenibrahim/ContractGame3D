# CONTRACT: SEOUL SHADOWS

A third-person, non-lethal stealth contract game set in a fictional night-time Seoul, for **PC and
Android**. You play a freelance operative: take contracts, find people and evidence without being
spotted, get paid, upgrade and work towards **$1,000,000**. On a phone, the optional **GPS Life
mode** turns real walking into progress: the distance you walk moves your operative along a route
in the city and earns XP, rewards, missions and collectibles.

Unity 6 (6000.6.6f1) · Universal Render Pipeline · Input System · AI Navigation.
Tested on a Linux PC (Intel UHD) and a Samsung Galaxy A22 5G (Android 13, Mali-G57 MC2).

All characters, companies and events are fictional. No one is harmed: guards warn, escort or
report you; contracts can fail and be retried.

More detail: [QA report](docs/QA_REPORT.md) · [GPS test plan](docs/GPS_TEST_PLAN.md) ·
[Android device testing](docs/ANDROID_DEVICE_TESTING.md).

---

## Playing

| Action | Keyboard / mouse | Gamepad | Touch (Android) |
|---|---|---|---|
| Move | WASD | Left stick | Floating joystick (left half) |
| Look | Mouse | Right stick | Drag on the right half |
| Sprint · crouch · jump | Shift · Ctrl · Space | L3 · B · A | Sprint / Crouch toggles · Jump |
| Interact (hold for terminals) | E | Y | Interact (tap or hold) |
| Map | M | View | Map button (pinch to zoom) |
| Pause / back | Esc | Start / B | Pause button |

**Flow:** Main menu → New game → operative (Alex Morgan: speed, Daniel Cross: stealth, Ryan Blake:
intelligence, $50,000) → **destination** (South Korea) → contract → district. The safehouse
terminal has three pages: Contracts, Upgrades (13 upgrades and outfits) and Finances.

**GPS Life mode** (Main menu → GPS Activity, or the strip at the bottom of the city HUD): the game
explains why it needs location, then asks Android. Start a session and walk; the operative
follows a walking route in the district. Pause or stop at any time. Only totals are kept
(distance, steps, XP): no coordinates are shown, stored or sent. Please watch your surroundings,
not the screen, and never use the phone while crossing a road.

**Saving:** automatic every minute, after contracts and purchases and when the app goes to the
background; manual save from the pause menu. A damaged save falls back to its backup.

---

## Content

| Area | State |
|---|---|
| Seoul · **Gangnam** (towers, Hangul signage, interiors, food alley, subway, parking) | Playable, 3 contracts |
| Seoul · **Myeongdong** (shopping streets, street-food carts, cathedral, station exit) | Playable, 2 contracts |
| Seoul · Bukchon, Hongdae; Busan, Jeju, Incheon, Gyeongju, Daegu | Listed on World Selection as "in development": not playable |
| GPS Life mode: distance, steps, XP, milestones, 6 GPS missions, checkpoints, 8 collectibles | Implemented; real-device walking still to be measured (see the GPS test plan) |
| Characters | Procedural placeholder humanoids (no rigged models imported) |
| Buildings | Generated from simple shapes with CC0 textures (no 3D model packs imported) |
| Audio | Original synthesized music, ambience and effects |

With two districts the $1,000,000 goal is not reachable in normal play yet.

---

## Graphics presets and performance

Settings → Graphics → Quality. The first launch picks a preset from the GPU; after that the
player's choice is kept. PCs and phones use different presets.

**Phones** (Android presets: own URP assets, LOD bias, crowd size, post-processing):

| Preset | Render scale | Shadows | Post-processing | Crowd | Galaxy A22 5G, Gangnam / Myeongdong |
|---|---|---|---|---|---|
| Low | 0.7 | 25 m, 512 | Off | 10 | **46–47 fps / 60 fps** |
| Medium | 0.85 | 35 m, 1024, soft | On | 16 | 22 fps / 25 fps |
| High | 1.0, 2x MSAA | 50 m, 2048, 2 cascades | On | 24 | 14 fps / 17 fps |

Measured with the in-game benchmark on one phone (Mali-G57 MC2, 3.6 GB); other phones will
differ. Menus are capped at 30 fps on phones to save battery. Phones ignore V-Sync, so the FPS
target setting applies there.

**PC** (Intel UHD i5-10300H, 1080p, Linux build): see the [QA report](docs/QA_REPORT.md) for the
current numbers.

---

## Building

**Android** (APK, IL2CPP, ARM64, Android 8.0+): `CONTRACT > Build > Build Android APK (device test)`, or headless:

```
Unity -batchmode -quit -projectPath . -executeMethod Contract.EditorTools.BuildPipeline.BuildAndroidCI
```

Output: `Builds/Android/ContractSeoulShadows.apk`. Install and test: see
[Android device testing](docs/ANDROID_DEVICE_TESTING.md).

**Linux:** `CONTRACT > Build > Build Linux Standalone (Release)` or
`-executeMethod Contract.EditorTools.BuildPipeline.BuildLinuxCI` →
`Builds/PC/Contract_SeoulShadows_Linux/`.

**Windows:** install *Windows Build Support* for 6000.6.6f1 in Unity Hub, then
`CONTRACT > Build > Build PC Standalone (Release)`. Not built or tested on this machine.

**Benchmark a build** (never touches the player's save):

```
./Contract_SeoulShadows.x86_64 -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -contract-benchmark [-contract-quality 0|1|2]
adb shell am start -n com.contractgames.seoulshadows/com.unity3d.player.UnityPlayerGameActivity -e unity '"-contract-benchmark -contract-quality 0"'
```

It plays menu → operative → destination → contract → Gangnam tour → travel to Myeongdong →
Myeongdong tour, and logs frame-time statistics (`[Benchmark]` lines) and `benchmark.txt`.

---

## Testing

```
Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults playmode.xml
Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults editmode.xml
```

229 Play Mode and 7 Edit Mode tests: boot, player, camera, district, NPCs, interaction, map,
contracts, economy, upgrades, saving, audio, quality; GPS (simulated location and step sources:
10 m to 1 km walks, vehicles, drift, signal gaps, pause / resume, checkpoints, collectibles,
save / load); destinations and Myeongdong; touch controls driven by a simulated touchscreen
through the real EventSystem; mobile HUD and settings; phone presets; every screen at six aspect
ratios; Android build settings and manifest. Run **with graphics** (no `-nographics`). Tests use a
separate save folder.

---

## Regenerating content

Most content is generated by editor builders. `CONTRACT > Rebuild Everything` runs them in order
(about 4 minutes):

1. Game database · upgrade catalogue · audio · activity (GPS) configuration
2. Gangnam district → population and navmesh → GPS walking route → map
3. Mission / terminal UI → menus and HUD → GPS activity UI → touch controls
4. Myeongdong (generated from the finished GameWorld scene) → destinations → World Selection
5. Quality tiers → Android presets and LOD culling → occlusion culling

Edit the builders (`Assets/Editor`, `Assets/Editor/World`), not the generated scenes, or the next
rebuild overwrites the change. Old prototype tools are archived in `Assets/Editor/Legacy~`.

---

## Code layout

| Folder | Contents |
|---|---|
| `Scripts/Core` | GameManager, session, settings, events, scene loader, input reader (keyboard, gamepad and touch), input hints, haptics |
| `Scripts/Activity` | GPS: location tracker and sources, walking-distance filter, step counter, rewards, GPS world (checkpoints, areas, collectibles), activity controller, route |
| `Scripts/Data` | ScriptableObject types (characters, regions, destinations, missions, upgrades, audio) |
| `Scripts/Player`, `Camera` | Controller (keyboard / touch / GPS route driver), rig, footsteps, third-person camera |
| `Scripts/NPCs`, `Interaction` | Civilians, guards, mission subjects, interactables |
| `Scripts/Missions`, `Regions`, `Economy`, `Progression` | Contracts and GPS missions, regions and destinations, money ledger, upgrades |
| `Scripts/SaveSystem` | JSON saves (v8), validation, atomic writes, backup |
| `Scripts/UI`, `UI/Touch`, `Map` | Menus, HUD, settings, World Selection, GPS dashboard, touch controls, safe area, minimap / full map |
| `Scripts/Performance`, `Bootstrap` | PC and phone presets, crowd budget, boot, benchmark |

---

## Manual steps / not included

- **Rigged characters:** none imported. Assign a rigged prefab to `CharacterData.characterPreviewPrefab`
  and an Animator Controller on the player's `PlayerCharacterApplier`; the placeholder is used until then.
- **More districts:** the destinations exist as data; each needs a district builder like
  `MyeongdongBuilder` and its scene in the build (`RegionData.sceneName`, `districtBuilt`).
- **Windows build support** module (see Building).
- **Release signing:** the APK uses Unity's debug keystore. A Play Store release needs your own
  keystore (Player Settings → Publishing Settings) and an Android App Bundle.

## Credits

Korean typeface: Noto Sans KR (SIL Open Font License 1.1, `Assets/UI/Fonts/NotoSansKR/OFL.txt`).
Brick, roof-tile, paving, granite and facade textures: Poly Haven (CC0, `Assets/Art/CC0/PolyHaven/LICENSE.md`).
Everything else (meshes, icons, music, sound effects) is generated by the project's own editor scripts.
