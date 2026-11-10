# Android device testing (Ubuntu)

The workflow used to test CONTRACT: SEOUL SHADOWS on a real phone (Samsung Galaxy A22 5G,
Android 13). Package: `com.contractgames.seoulshadows`. Activity:
`com.unity3d.player.UnityPlayerGameActivity`.

`adb` ships with Unity's Android module, so nothing else needs installing:

```
ADB=~/Unity/Hub/Editor/6000.6.6f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
```

(or `sudo apt install adb` and use `adb` directly).

---

## 1. Enable Developer Options

Settings → About phone → Software information → tap **Build number** seven times, then enter the
phone's PIN. "Developer options" appears in Settings (on Samsung: at the bottom of the list).

## 2. Enable USB debugging

Settings → Developer options → **USB debugging** on. Optional: "Stay awake" (screen stays on
while charging), which helps with long test sessions.

## 3. Connect the phone to Ubuntu

Use a data-capable USB cable. On the phone, choose "File transfer" or leave the default. The
first time, the phone asks **"Allow USB debugging?"** with the computer's key fingerprint:
tick "Always allow from this computer" and confirm.

If Ubuntu does not see the phone, check `lsusb` (Samsung shows as `04e8:6860`). If adb lists the
device as "no permissions", install the udev rules: `sudo apt install android-sdk-platform-tools-common`,
then unplug and replug the phone.

## 4. Verify the device with ADB

```
$ADB devices -l
# R9WR60RKCEJ  device usb:1-2 product:a22xnseea model:SM_A226B ...
```

"unauthorized" means the phone's prompt was not accepted yet; "device" means ready.
Useful device facts:

```
$ADB shell getprop ro.build.version.release        # Android version
$ADB shell grep MemTotal /proc/meminfo              # RAM
$ADB shell wm size                                  # screen resolution
$ADB shell dumpsys battery | grep -E "level|temperature"
$ADB shell dumpsys thermalservice | grep "Thermal Status"
```

## 5. Build the APK

In Unity: `CONTRACT > Build > Build Android APK (device test)`, or headless (about 4–6 min):

```
Unity -batchmode -quit -projectPath . -executeMethod Contract.EditorTools.BuildPipeline.BuildAndroidCI
```

Output: `Builds/Android/ContractSeoulShadows.apk` (IL2CPP, ARM64, Android 8.0+, Vulkan with GLES3
fallback, ASTC). Add `-development` after the method for a development build (profiler and
debugging).

## 6. Install the APK

```
$ADB install -r Builds/Android/ContractSeoulShadows.apk
```

`-r` keeps the app's data (the career save and settings). To start from scratch (first-launch
quality choice, permission prompts, an empty career), uninstall first:
`$ADB uninstall com.contractgames.seoulshadows`. **This deletes the career on the phone.**

## 7. Launch the game

Tap the icon, or:

```
$ADB shell monkey -p com.contractgames.seoulshadows -c android.intent.category.LAUNCHER 1
```

Launch arguments go in the intent extra `unity` (the game reads it like a command line):

```
# benchmark (separate save folder; quits when done)
$ADB shell am start -n com.contractgames.seoulshadows/com.unity3d.player.UnityPlayerGameActivity -e unity '"-contract-benchmark -contract-quality 0"'
# GPS self-test: 60 s of location status, fix count and accuracy (never coordinates)
$ADB shell am start -n com.contractgames.seoulshadows/com.unity3d.player.UnityPlayerGameActivity -e unity '"-contract-gps-selftest"'
```

## 8. Grant location permissions

The normal way is in the game: Main menu → GPS Activity (or Settings → GPS → GPS setup). The game
explains why it needs location, then Android asks: choose **While using the app** and keep
**Precise** on. Do the same for "Physical activity" (the step counter).

For test cases, permissions can be checked, granted or revoked from the computer:

```
$ADB shell dumpsys package com.contractgames.seoulshadows | grep -E "ACCESS_FINE_LOCATION|ACTIVITY_RECOGNITION"
$ADB shell pm revoke com.contractgames.seoulshadows android.permission.ACCESS_FINE_LOCATION   # "denied" case
$ADB shell pm grant  com.contractgames.seoulshadows android.permission.ACCESS_FINE_LOCATION
```

Device location on/off is the phone's own setting (quick-settings tile). Test both.

## 9. Test GPS movement outdoors, in a safe area

- Choose a **public, safe, traffic-free** place: a park path or a wide pavement. Never a road,
  a private property or a restricted area.
- Measure the route independently first: a running track (400 m per lap), or a route drawn in a
  map app's "measure distance" tool, or distance markers in a park.
- Start the session, put the phone in your pocket, and **walk while looking at your surroundings,
  not the screen**. Stop the session where the route ends.
- Note: the route's measured length, the game's real distance, virtual distance, steps,
  battery before and after, and the weather (clear sky vs between buildings).
- Expect several metres of difference, more between tall buildings. The filter deliberately
  under-counts rather than over-counts.

The cases to run are in the [GPS test plan](GPS_TEST_PLAN.md).

## 10. Test step counting

The Galaxy A22 has a hardware step counter. Grant "Physical activity", start a session, and walk
100 counted steps. The session's step count should be close (the sensor may report in batches
of a few seconds). With the permission denied, the game should say so in Settings → GPS and keep
counting distance from GPS alone.

## 11. Inspect logs with ADB

```
$ADB logcat -c                                 # clear old lines
$ADB logcat -s Unity                           # the game's log only
$ADB logcat -v time Unity:V AndroidRuntime:E CRASH:E DEBUG:F '*:S' > device.log   # plus crashes, with timestamps
```

The game tags its own lines: `[GPS]`, `[GPS selftest]`, `[Performance]`, `[Benchmark]`.
Known harmless line: `ClassNotFoundException ... AssetPackManager` (Unity probing for Play Asset
Delivery, which this APK does not use).

## 12. Identify crashes and performance problems

- **Crashes:** look for `AndroidRuntime: FATAL EXCEPTION`, `F DEBUG` (native crash with a
  backtrace) or `E/Unity` exceptions in the log above. `$ADB shell dumpsys dropbox --print` keeps
  recent crash reports.
- **Frame rate:** run the benchmark (step 7) for each quality level and compare the
  `Gangnam overall` / `Myeongdong overall` lines. Frame times on a 60 Hz phone come in steps of
  16.7 ms (60 / 30 / 20 fps), so look at the median and p95, not only the average.
- **Heat and battery:** read `dumpsys battery` (temperature in tenths of °C) and
  `dumpsys thermalservice` before and after a 15–20 minute session. A thermal status above 0
  means the phone is throttling.
- **Memory:** the benchmark's `Memory:` line; or `$ADB shell dumpsys meminfo com.contractgames.seoulshadows`.
- **Screenshots:** `$ADB exec-out screencap -p > screen.png`.
- **Touch input from the computer** (one finger only; real multi-touch needs real fingers):
  `$ADB shell input tap X Y`, `$ADB shell input swipe X1 Y1 X2 Y2 300`,
  `$ADB shell input motionevent DOWN X Y` / `MOVE X Y` / `UP X Y` for a held joystick.

## 13. Repeat after every modification

1. Run the automated tests in the editor (PlayMode and EditMode, see the README).
2. Build the APK (step 5) and install with `-r` (step 6).
3. Clear the log, launch, and play the changed feature; run the benchmark if anything visual or
   performance-related changed.
4. Check the log for new warnings or errors (step 11).
5. Write the result into the [QA report](QA_REPORT.md) or the [GPS test plan](GPS_TEST_PLAN.md):
   device, build date, what was tested, measured values.

Never claim a device test that was not run on a connected phone.
