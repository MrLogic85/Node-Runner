# Localization

How Node Runner's text is written and translated. The game ships in
English only, but all text goes through Godot's translation system, so
adding a language needs no code changes. The English text is the
translation key (gettext msgid). `docs/ARCHITECTURE.md` → "UI text and
translation" owns how text crosses the layers, and `.claude/rules/ui.md`
how UI code shows it.

## Text rules

- Text written in a scene is English and is its own translation key.
- Text the player wrote, such as a creation's or part's own name, is never
  translated; a default name such as "Joint 2" is. A creation's default
  name, such as "Untitled Creation", is saved in the player's language
  when it is made and is their own text from then on.
- Godot translates whole messages only, so text is never put together in
  code.
- Counted text is one whole sentence per plural form, and Godot picks the
  form; never add an "s" in code.
- Numbers are arguments, never part of the English: "{0} m", not "2.5 m",
  so the language's own digits show.
- Uppercase is display only, applied after translating by the TextServer
  in the current language, never by .NET casing.
- A translation context tells two meanings of the same English apart,
  such as "Run" the verb and "Run" the noun.

## The template

`project/locale/messages.pot` lists every text the player can see, with
the files it comes from; translators start from it. It is generated,
never edited by hand. `TranslationTemplateTests` fails when it is out of
date; after changing shown text, regenerate it and commit it with the
change:

```sh
NODE_RUNNER_UPDATE_POT=1 dotnet test tests/NodeRunner.Ui.Tests --filter TranslationTemplateTests
```

The test builds the template from scene text (with a node's
`translation_context` as msgctxt), every `UiText`, and constant text set
in code that reaches a text property or popup, followed through locals,
fields, parameters and returns. It leaves out text without letters, text
not from a literal (such as a creation's name), nodes with auto-translate
Disabled, `Id`s, and the galleries, which are developer tools. It fails
on a `UiText` it cannot trace to literal text and on an English text used
both alone and counted.

Godot's own generator runs only from the editor, reads only scenes and
GDScript, and cannot be checked in a test, so it would miss our
components' text properties and all text set in C#.

## Adding a language

A language is a `project/locale/<language>.po` file made from the
template; the `add-language` skill has the steps. In a translation, each
`{0}`, `{1}` … is used exactly once, in every plural form
(`TranslationPlaceholdersTests` checks it), and a plural entry has one
`msgstr[n]` per form of the language. Godot reads the system language at
start, so checking another language means restarting the game in it.

When the template changes, update each language with Poedit (Update from
POT) or `msgmerge -U <language>.po messages.pot`.

## Text server data

`internationalization/locale/include_text_server_data` is off, since
English does not need it; turn it on when adding a language. It adds
about 5 MB of ICU data to the APK. Without it, an exported build cases
text like English in every language (Turkish "i" becomes "I", not "İ")
and cannot break lines in languages without spaces, such as Thai or
Chinese.
