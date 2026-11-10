# GPS test plan and results

Status legend:

- **Editor**: covered by automated Play Mode tests with the simulated location and step sources
  (recorded-style traces with noise, drift, gaps and vehicles). Proves the logic, not the phone's GPS.
- **Device**: observed on a real phone (Samsung Galaxy A22 5G, Android 13), with the date.
- **Pending**: needs an outdoor walk on a real phone. Not done yet.

GPS accuracy is never perfect. The walking filter is designed to **under-count rather than
over-count** (it ignores fixes worse than the accuracy threshold, vehicle speeds and stationary
drift), so a few metres to ~10 % less than the measured route is expected; more than the route is a bug.

## Results

| # | Case | How to run | Editor | Device |
|---|---|---|---|---|
| 1 | Permission granted | Allow "While using the app" + Precise in the GPS setup screen | `AccessScreen_ExplainsAndRunsAGpsCheck` | **Observed 2026-11-10**: permissions granted by the tester; GPS self-test reports location and activity permission `Granted` |
| 2 | Permission denied | Deny in the dialog, or `pm revoke` (see device testing) | `Dashboard_ExplainsWhatIsMissing_OnAPhoneWithoutGps` | Partly: before granting, Settings → GPS showed "Not asked yet" / "Needs the Physical activity permission" and the setup screen explained what to choose. The deny path was not run |
| 3 | GPS disabled | Location off in quick settings, start a session | `LocationSwitchedOff_IsReported_AndRecoveredWhenSwitchedOn` | **Observed 2026-11-10**: location off with permission granted → status `ServiceDisabled`, re-checked every 2 s, clean stop, no crash, no coordinates logged |
| 4 | Signal unavailable / lost | Start indoors, or walk through an underpass | `SignalGap_StraightAcross_IsCountedWhenPlausible`, `InaccurateFixes_AreIgnored` | Pending |
| 5 | Walking 10 m | Walk 10 m (measured with a tape or paving slabs) | `ShortWalk_10m_WithSteps_IsCounted`, `ShortWalk_10m_GpsOnly_IsNeverOvercounted` | Pending |
| 6 | Walking 100 m | One straight stretch of a measured path | `StraightWalk_100m` | Pending |
| 7 | Walking 500 m | 1.25 laps of a 400 m track | `Walking_WithSteps_IsAccurate_EvenWithRoughGps`, milestone `walk_500m` ($5,000 + 100 XP) | Pending |
| 8 | Walking 1 km | 2.5 laps of a 400 m track, or a measured park loop | `Square_1km_GoodAndRoughSignal`, milestone `walk_1km` (unlocks Myeongdong) | Pending |
| 9 | Step counting | Walk 100 counted steps during a session | `SessionSteps_StartFromTheHardwareTotal_NotTheLifetimeCount`, `CounterReset_AfterAReboot_KeepsTheSessionSteps` | Partly: the phone has a step counter that the game detects; the tester's own session showed **21 steps / 21 m**, not compared with a counted walk |
| 10 | GPS checkpoint activation | Dashboard → Mark spot, walk 150 m away, come back | `MarkedCheckpoint_TriggersWhenThePlayerWalksAwayAndComesBack_Once`, `Checkpoint_IgnoresAStrayFix_AndNeedsTheWalkFirst` | Pending |
| 11 | Mission unlocking | Walk 100 m: "First Steps" completes and "Explorer" unlocks | `FirstSteps_Completes_UnlocksExplorer_WhoseProgressStartsAtUnlock` | Pending |
| 12 | Reward delivery | Milestones and collectibles pay once (money, XP) | `Milestones_PayOnce_EvenWhenEvaluatedAgainOrReloaded`, `Collectibles_AreDiscoveredInOrder_CollectedOnce` | Pending (a $1,000 balance appeared in the tester's career; its source was not verified) |
| 13 | Application pause and resume | Home button during a session, come back after 30 s | `GoingToTheBackground_PausesTheSessionAndSaysWhy`, `Tracker_StopsInTheBackground_AndResumes` | Pending |
| 14 | Save and load | Walk, close the app, reopen | `SaveAndLoad_KeepsGpsProgress_AndPaysNothingAgain`, `InterruptedSession_IsRestoredPaused_AndTotalsSurviveSaving` | **Observed 2026-11-10**: the tester's totals (21 m, 21 steps, GPS Life mode) survived closing the app, reinstalling the APK with `-r` and relaunching |
| 15 | GPS accuracy changes | Walk from open sky into a street with tall buildings | `Square_1km_GoodAndRoughSignal`, `CorrelatedDrift_GpsOnly_IsBounded` | Pending |
| 16 | Standing still | Stand still with GPS on for 2 minutes | `StandingStill_DriftIsNotWalking`, `CorrelatedDrift_WithNoSteps_IsZero` | Pending |
| 17 | Vehicle | Take a bus or car ride during a session (as a passenger) | `VehicleRide_IsNotCounted_ButTheWalksAroundItAre` | Pending |

## Outdoor test protocol (cases 4–17)

1. Safe public place without traffic; measure the route independently first (running track,
   park distance markers, or a map app's distance tool).
2. Phone charged; note battery level and temperature (`dumpsys battery`).
3. Start the session in the game, put the phone away, walk **without looking at the screen**.
4. At the end: note real distance, virtual distance, steps, rewards; stop the session.
5. Battery again; save the log (`adb logcat -s Unity`; it contains status and accuracy, never coordinates).
6. Fill a row:

| Date | Route (measured) | Game real distance | Error | Steps (game / counted) | Weather / surroundings | Battery used |
|---|---|---|---|---|---|---|
| | | | | | | |

## Player safety (design checks)

| Requirement | How the game meets it | Verified |
|---|---|---|
| Pause GPS activity at any time | Pause / Stop buttons on the dashboard; the app going to the background pauses the session | Editor tests; dashboard seen on the phone |
| Never require interaction while crossing roads | Sessions run with the phone in the pocket: distance, steps, checkpoints and rewards need no taps; notifications only report | Design |
| No objectives in dangerous, restricted or private places | There are no fixed real-world objective locations: checkpoints are spots the player marks themselves, collectibles are found by walking into new ~100 m areas anywhere | Design + `AreaKeys_AreSaltedHashes_NotLocations` |
| Reminder to watch the surroundings | Shown on the dashboard: "Watch your surroundings, not the screen — never while crossing a road" | Seen on the phone |
| No unsafe movement encouraged | Vehicle speeds are not counted; no timers on walking challenges | Editor tests |
| Privacy | Only totals and salted area hashes are saved; no coordinates shown, logged, stored or uploaded | Editor tests + self-test log on the phone |
