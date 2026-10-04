using System.Globalization;
using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// Turns a view-model's <see cref="UiText"/> into the player's language with Godot's own
/// translation (#682): <c>Translate</c>, or <c>TranslatePlural</c> to pick the plural form, then
/// fills in the arguments. Numbers are written with a point and Western digits, then
/// <c>FormatNumber</c> swaps the digits for the language's own, as Godot's number fields do. Not
/// <c>Tr</c>/<c>TrN</c>: they follow the node's own auto-translate mode, which is off on a
/// component part that shows a text source, so they would return the English unchanged.
/// </summary>
public static class UiTextTranslation
{
    /// <summary>Shows <paramref name="text"/> on <paramref name="label"/> and keeps it in the current language.</summary>
    public static void ShowText(this UiLabel label, UiText text) => label.TextSource = Source(text);

    /// <summary>Shows <paramref name="text"/> on <paramref name="button"/> and keeps it in the current language.</summary>
    public static void ShowText(this UiButton button, UiText text) => button.TextSource = Source(text);

    /// <summary>
    /// A text source for a component's <c>…Source</c> property, which asks it again when the
    /// language changes; null for no text, so the component shows its plain property.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(text))]
    public static Func<string>? Source(UiText? text) => text is null ? null : () => InLanguage(text);

    private static string InLanguage(UiText text)
    {
        var template = text.Plural is { } plural
            ? TranslationServer.TranslatePlural(text.Message, plural, text.Count, text.Context ?? string.Empty)
            : TranslationServer.Translate(text.Message, text.Context ?? string.Empty);
        var args = text.Args.Select(Shown).ToArray();
        try
        {
            return string.Format(CultureInfo.InvariantCulture, template, args);
        }
        catch (FormatException)
        {
            // UiText checked the English placeholders, so the translation is at fault. Show the English
            // instead, picked as TranslatePlural picks it when there is no translation.
            GD.PushError($"The translation \"{template}\" of \"{text.Message}\" has a placeholder its {args.Length} arguments do not fill.");
            var english = text.Plural is { } englishPlural && text.Count != 1 ? englishPlural : text.Message;
            return string.Format(CultureInfo.InvariantCulture, english, args);
        }
    }

    private static object Shown(object arg) => arg switch
    {
        UiText nested => InLanguage(nested),
        FixedNumber number => Digits(number.Value.ToString($"F{number.Decimals}", CultureInfo.InvariantCulture)),
        int or long => Digits(((IFormattable)arg).ToString(null, CultureInfo.InvariantCulture)),
        _ => arg,
    };

    private static string Digits(string number) => TranslationServer.FormatNumber(number, TranslationServer.GetLocale());
}
