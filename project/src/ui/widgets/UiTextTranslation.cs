using System.Globalization;
using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// Turns a view-model's <see cref="UiText"/> into the player's language with Godot's own
/// translation (#682): <c>Tr</c>, or <c>TrN</c> to pick the plural form, then fills in the
/// arguments. Numbers keep the invariant format they had before.
/// </summary>
public static class UiTextTranslation
{
    /// <summary>Shows <paramref name="text"/> on <paramref name="label"/> and keeps it in the current language.</summary>
    public static void ShowText(this UiLabel label, UiText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        label.TextSource = () => InLanguage(label, text);
    }

    private static string InLanguage(Node node, UiText text)
    {
        var template = text switch
        {
            { Plural: { } plural, Context: { } context } => node.TrN(text.Message, plural, text.Count, context),
            { Plural: { } plural } => node.TrN(text.Message, plural, text.Count),
            { Context: { } context } => node.Tr(text.Message, context),
            _ => node.Tr(text.Message),
        };
        var args = text.Args.Select(arg => arg is UiText nested ? InLanguage(node, nested) : arg).ToArray();
        try
        {
            return string.Format(CultureInfo.InvariantCulture, template, args);
        }
        catch (FormatException)
        {
            // UiText checked the English placeholders, so the translation is at fault. Show the English
            // instead, picked as Godot's TrN picks it when there is no translation.
            GD.PushError($"The translation \"{template}\" of \"{text.Message}\" has a placeholder its {args.Length} arguments do not fill.");
            var english = text.Plural is { } plural && text.Count != 1 ? plural : text.Message;
            return string.Format(CultureInfo.InvariantCulture, english, args);
        }
    }
}
