---
name: change-saved-shape
description: Change what Node Runner saves in creation.json or progression.json, with the migration, tests, share dictionary and pins it needs. Use when a change adds, removes, renames or reinterprets a saved field, or changes what a saved value means.
---

# Change a saved shape

`docs/SAVE_FORMAT.md` → "Versions and migration" owns the rules.

1. Decide by "Changing a saved shape" whether the change needs a
   migration. If not, record why in "Versions and migration" and skip
   steps 3–5.
2. Change the record and its section in `docs/SAVE_FORMAT.md` →
   "`creation.json`" or "`progression.json`". `SaveFormatTests` then
   fails and prints where it wrote the new schema; copy that file over
   its `docs/save-schema/` copy and read the diff.
3. Append a `SaveMigration` to the file's list
   (`FileCreationRepository.Format` for `creation.json`,
   `FileProgressionRepository.Format` for `progression.json`), and list it
   in "Versions and migration" as the `creation.json` migrations are.
4. Add a test that loads a file in the previous version.
5. For `creation.json`, make the new version's share dictionary, then run
   again to build it in:

   ```bash
   NODE_RUNNER_UPDATE_SHARE_DICTIONARY=1 dotnet test tests/NodeRunner.App.Tests --filter ShareDictionaryTests
   dotnet test tests/NodeRunner.App.Tests
   ```

6. Rewrite the pins and read their diff; only what this change meant to
   alter may differ:

   ```bash
   NODE_RUNNER_UPDATE_PINS=1 dotnet test tests/NodeRunner.App.Tests --filter EveryPartPinTests
   git diff tests/NodeRunner.App.Tests/Repositories/Pins/
   dotnet test tests/NodeRunner.App.Tests   # the 0.13.0 fixtures must still load
   ```

7. Plan a manual check that an older save loads on a device
   (`plan-manual-tests`).
