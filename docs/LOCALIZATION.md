# Localization

How Node Runner's text is written and translated. The game is written in
English, and the English text is the translation key (gettext msgid).
`docs/ARCHITECTURE.md` → "UI text and translation" owns how text crosses
the layers, and `project/src/ui/AGENTS.md` how UI code shows it.

The game ships in English only (`docs/ROADMAP.md` → "Non-goals"), but all
text goes through Godot's translation system (#682), so adding a language
needs no code changes.

## Text rules

- Text written in a scene is English and is its own translation key.
- Text the player wrote, such as a creation's or part's own name, is never
  translated. A default name such as "Joint 2" is. A creation's default
  name, such as "Untitled Creation", is saved in the player's language when
  it is made and is their own text from then on (#757, #759).
- Godot translates whole messages only, so text is never put together in
  code (#773).
- Counted text is one whole sentence per plural form, and Godot picks the
  form; never add an "s" in code.
- Numbers are arguments, never part of the English: "{0} m", not "2.5 m".
  Godot swaps in the language's own digits (#756).
- Uppercase is display only and applied after translating, by the
  TextServer in the current language, never by .NET casing (#776).
- A translation context tells two meanings of the same English apart, such
  as "Run" the verb and "Run" the noun (#777).

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

Text without letters, such as "—" or "1", and text that does not come from
a literal, such as a creation's name, are left out. The test fails on a
`UiText` it cannot trace to literal text (an interpolated string, or a
non-literal plural or context) and on an English text used both alone and
counted.

The galleries (Component Gallery, Popup Gallery, Colors & Styles and
Toolbars) are developer tools, so they stay in English and are left out.
Preview text that a scene or component shows before the game fills it in,
such as "Generation 1", is in the template too; translating it is
harmless.

### Why not Godot's generator

Godot's own POT generator (Project Settings → Localization → Template
Generation) runs only from the editor UI and reads only scenes and
GDScript. It would miss our components' text properties, every `UiText` and
all text set in C#, and it cannot be checked in a test.

## Adding a language

1. Create `project/locale/<language>.po` from `messages.pot`, for example
   with Poedit (New from POT) or `msginit --locale=sv -i messages.pot -o
   sv.po`. Set the `Language` and `Plural-Forms` headers for the language.
2. Translate every entry. Keep `{0}`, `{1}` and so on: each one must be
   used exactly once. A plural entry has one `msgstr[n]` per form of the
   language.
3. Register the file in Project Settings → Localization → Translations. It
   is saved as `internationalization/locale/translations` in
   `project/project.godot`.
4. Turn on the text server data (below).
5. Run the game with the device set to the language. Godot reads the
   system language when the game starts, so restart it after changing the
   device language. A change through `TranslationServer.SetLocale` updates
   open screens at once.

When the template changes, update each language with Poedit (Update from
POT) or `msgmerge -U <language>.po messages.pot`.

## Text server data

`internationalization/locale/include_text_server_data` is off, since English
does not need it. Turn it on in Project Settings → Internationalization →
Locale when adding a language. It adds about 5 MB of ICU data to the APK.
Without it, an exported build cases text like English in every language
(Turkish "i" becomes "I", not "İ") and cannot break lines in languages
without spaces, such as Thai or Chinese.
