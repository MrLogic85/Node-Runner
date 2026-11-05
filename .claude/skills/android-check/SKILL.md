---
name: android-check
description: Export, install and run the Node Runner debug build on an Android phone or emulator, load a test creation, capture screenshots and logcat as evidence, and leave the device as it was found. Use when a manual test plan has Android steps or a change touches export, install, input, file paths or performance.
---

# Android check

Rules: `docs/MANUAL_TESTING.md` → "Android checks" and
`docs/RELEASING.md` → "Android export". Each shell call starts afresh:
every block sets the variables it uses.

1. **Pick the device** per "Android checks". Follow `LOCAL_CONFIG.md`'s
   device rules, and any local skill it names, until step 7 is done; a
   subagent leaves any device lock to its caller. Note the serial of an
   emulator you start. More than one device in `adb devices -l`: add
   `ANDROID_SERIAL=<serial>` to each block.
2. **Record how you found it**, for step 7. The block prints one state:
   `not-installed`, `never-ran` (installed, no saved files yet) or
   `before.txt` (the creation folders). It prints none: adb or `run-as`
   failed, or the app holds saves but no creations; stop and ask the
   human. It refuses while an earlier check's state is left: finish that
   check's step 7 first.

   ```bash
   pkg=dev.mrlogic85.noderunner.debug s=.scratch/android
   mkdir -p .scratch && rm -f "$s-packages.txt" "$s-files.txt" "$s-list.txt"
   ! ls "$s-not-installed" "$s-never-ran" "$s-before.txt" 2> /dev/null | grep -q . &&
     adb get-state | grep -qx device &&
     adb shell pm list packages "$pkg" > "$s-packages.txt" &&
     if ! grep -qx "package:$pkg" "$s-packages.txt"; then touch "$s-not-installed"
     elif adb shell run-as "$pkg" ls files > "$s-files.txt"; then
       if [ ! -s "$s-files.txt" ]; then touch "$s-never-ran"
       elif grep -qx creations "$s-files.txt" &&
         adb shell run-as "$pkg" ls files/creations > "$s-list.txt"; then
         mv "$s-list.txt" "$s-before.txt"
       fi
     fi
   ls "$s"-not-installed "$s"-never-ran "$s"-before.txt 2>/dev/null
   ```

3. **Export, install and launch.** The block deletes the old APK, so a
   failed export cannot install it. `logcat -c` clears the whole device
   log, not just the app's.

   ```bash
   pkg=dev.mrlogic85.noderunner.debug
   rm -f build/node-runner-debug.apk
   # the debug export from docs/RELEASING.md → "Android export"
   test -f build/node-runner-debug.apk &&
     adb install -r build/node-runner-debug.apk &&
     adb logcat -c &&
     adb shell monkey -p "$pkg" -c android.intent.category.LAUNCHER 1
   ```

   `install -r` keeps the app's saves. A signature mismatch: ask the
   human; uninstalling would delete their creations.
4. **Load a test creation** if the plan needs one, such as
   `tests/NodeRunner.App.Tests/Repositories/SaveExamples/0.13.0/walker.creation.json`
   (`docs/SAVE_FORMAT.md` → "Layout"). Its folder is its `id` without
   dashes. The block refuses an id the device already has: pick another
   creation. Its relaunch makes the app read its creations again. In-app
   alternative: a share code (`docs/SAVE_FORMAT.md` → "Share code").

   ```bash
   pkg=dev.mrlogic85.noderunner.debug f=<creation.json>
   id=$(jq -r .id "$f" | tr -d -)
   [[ $id =~ ^[0-9a-f]{32}$ ]] &&
     adb push "$f" /data/local/tmp/nr-creation.json &&
     adb shell run-as "$pkg" sh -c "'test ! -e files/creations/$id && mkdir -p files/creations/$id && cp /data/local/tmp/nr-creation.json files/creations/$id/creation.json'" &&
     adb shell am force-stop "$pkg" &&
     adb shell monkey -p "$pkg" -c android.intent.category.LAUNCHER 1
   ```

5. **Run the steps and capture evidence.**
   - Screenshot: `adb exec-out screencap -p > .scratch/<step>.png`. Look
     at each before judging the step and before sharing it. It captures
     the whole screen: retake or crop one that shows notifications or
     another app.
   - `adb shell input tap X Y` and `adb shell input swipe X1 Y1 X2 Y2 MS`
     take the screenshot file's pixels: scale a point read off a
     downsized view.
   - Collect the log and check the app is still running:

     ```bash
     adb logcat -d -s godot:V AndroidRuntime:E DEBUG:F libc:F > .scratch/logcat.txt
     adb shell pidof dev.mrlogic85.noderunner.debug
     ```

   - Read the log for `ERROR`, `SCRIPT ERROR`, exceptions and fatal
     signals. Godot logs its warnings at level `E` too.
6. **Record the run** in the PR's **How to verify** per
   `docs/MANUAL_TESTING.md` → "What belongs in a PR", with the device
   model (`adb shell getprop ro.product.model`) and Android version
   (`adb shell getprop ro.build.version.release`).
7. **Leave the device as you found it**, also when the check fails or
   stops early. The block stops the app first, since the Build screen
   saves on exit, then undoes what step 2 recorded. It restores only the
   install, the creation folders and the files above: don't open the
   human's own creations, since the app may save them.

   ```bash
   pkg=dev.mrlogic85.noderunner.debug s=.scratch/android
   adb shell am force-stop "$pkg"
   adb shell rm -f /data/local/tmp/nr-creation.json
   if [ -f "$s-not-installed" ]; then
     adb shell pm list packages "$pkg" > "$s-now.txt" &&
       { ! grep -qx "package:$pkg" "$s-now.txt" || adb uninstall "$pkg"; } && rm -f "$s"-*
   elif [ -f "$s-never-ran" ]; then adb shell pm clear "$pkg" && rm -f "$s"-*
   elif [ -f "$s-before.txt" ]; then
     adb shell run-as "$pkg" ls files/creations | grep -E '^[0-9a-f]{32}$' | grep -vxFf "$s-before.txt" |
       while read -r id; do adb shell run-as "$pkg" rm -r "files/creations/$id" < /dev/null; done
     adb shell run-as "$pkg" ls files/creations > "$s-after.txt" &&
       diff "$s-before.txt" "$s-after.txt" && rm -f "$s"-*
   else echo "step 2 recorded no state: ask the human"
   fi
   ```

   Done when the `.scratch/android-*` files are gone. Then release the
   device per `LOCAL_CONFIG.md` (a subagent leaves its caller's lock),
   stop an emulator you started (`adb -s <serial> emu kill`), and delete
   `build/node-runner-debug.apk` and the other scratch files.
