---
name: add-language
description: Add a translation of Node Runner into a new language, from the .po file to checking the game in it. Use when asked to add, start or register a language or translation.
---

# Add a language

Rules: `docs/LOCALIZATION.md`.

1. Check `project/locale/messages.pot` is current. The test fails:
   regenerate it per "The template".

   ```bash
   dotnet test tests/NodeRunner.Ui.Tests --filter TranslationTemplateTests
   ```

2. Create `project/locale/<language>.po` from the template, in Poedit
   (New from POT) or with gettext. `--no-translator` keeps your name and
   email out of the committed file. Either way, check the headers and set
   `Language` and `Plural-Forms` for the language.

   ```bash
   msginit --no-translator --locale=<language> -i project/locale/messages.pot -o project/locale/<language>.po
   ```

3. Translate every entry per "Adding a language". Check the header and
   plural forms, then the `{0}` placeholders:

   ```bash
   msgfmt --check -o /dev/null project/locale/<language>.po
   dotnet test tests/NodeRunner.Ui.Tests --filter TranslationPlaceholdersTests
   ```

4. Register the file in Project Settings → Localization → Translations
   (`internationalization/locale/translations` in `project/project.godot`),
   and turn on the text server data ("Text server data").
5. Check the game in the language with the `desktop-check` and
   `android-check` skills, with the system set to it. Leave the wording to
   the human: a native speaker judges it.
