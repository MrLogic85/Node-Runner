# Localization

How Node Runner's text gets into other languages. The game is written in
English, and the English text is the translation key (gettext msgid).
`docs/ARCHITECTURE.md` → "UI text and translation" owns how text crosses
the layers; `docs/UI_DIRECTION.md` → "Text and translation" owns the rules
for showing it.

## The template

`project/locale/messages.pot` lists every text the player can see, with the
files it comes from. Translators start from it. It is generated, so never
edit it by hand.

`TranslationTemplateTests` builds the template from the code and scenes and
fails when the committed file is out of date. After changing shown text,
regenerate it:

```sh
NODE_RUNNER_UPDATE_POT=1 dotnet test tests/NodeRunner.Ui.Tests --filter TranslationTemplateTests
```

Then commit the new `messages.pot` with the change.

The template holds:

- **Scene text:** `text`, `tooltip_text`, `placeholder_text`, `title`, and
  our components' exported text properties, such as `LabelText` or
  `StepLabels`. A node's own `translation_context` becomes its msgctxt.
  Nodes with auto-translate Disabled, and their children that inherit it,
  are left out. A player's own text field value (`TextValue`) is never text
  to translate.
- **`UiText`:** the message of every `UiText.Plain`, `Format` and `Counted`,
  with its plural and its `InContext` context.
- **Text set in code:** constant text that reaches a Godot text property,
  one of our components' text properties or a popup's text. The test
  follows it through locals, fields, parameters, returns, records and
  `?:`/`??`/switch branches.

Text without letters, such as "—" or "1", is left out. The test fails when
it cannot read a message, for example a counted text or context that is not
literal, or when one English text is used both alone and counted.

The galleries (Component Gallery, Popup Gallery, Colors & Styles and
Toolbars) are developer tools, so they stay in English and are left out.
Preview text that a scene or component shows before the game fills it in,
such as "Generation 1", is in the template too; translating it is
harmless.

### Why not Godot's generator

Godot's own POT generator (Project Settings → Localization → Template
Generation) runs only from the editor UI, has no command-line or script
entry, and reads only scenes and GDScript. It would miss our components'
text properties, every `UiText` and all text set in C#, and it cannot be
checked in a test. So we generate the template ourselves.

## Adding a language

No code changes are needed.

1. Create `project/locale/<language>.po` from `messages.pot`, for example
   with Poedit (New from POT) or `msginit --locale=sv -i messages.pot -o
   sv.po`. Set the `Language` and `Plural-Forms` headers for the language.
2. Translate every entry. Keep `{0}`, `{1}` and so on: each one must be
   used exactly once. A plural entry has one `msgstr[n]` per form of the
   language.
3. Register the file in Project Settings → Localization → Translations. It
   is saved as `internationalization/locale/translations` in
   `project/project.godot`.
4. Run the game with the device set to the language. Godot picks the
   language from the system; text on open screens updates when the
   language changes.

When the template changes, update each language with Poedit (Update from
POT) or `msgmerge -U <language>.po messages.pot`.

## Text server data

`internationalization/locale/include_text_server_data` is on. It adds about
3 MB of ICU data to the export. Without it, an exported build cases text
like English in every language (Turkish "i" becomes "I", not "İ") and
cannot break lines in languages without spaces, such as Thai or Chinese.
