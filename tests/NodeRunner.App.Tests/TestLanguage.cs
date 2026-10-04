using System.Globalization;
using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests;

/// <summary>Stand-ins for Godot's translation, for workflows that save text in the player's language.</summary>
internal static class TestLanguage
{
    /// <summary>The English text, as Godot writes it with no translation loaded.</summary>
    public static string English(UiText text) =>
        string.Format(
            CultureInfo.InvariantCulture,
            text.Plural is { } plural && text.Count != 1 ? plural : text.Message,
            text.Args.Select(arg => arg is UiText nested ? English(nested) : arg).ToArray());
}
