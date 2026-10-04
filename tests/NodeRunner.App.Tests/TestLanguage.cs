using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests;

/// <summary>Stand-ins for Godot's translation, for workflows that save text in the player's language.</summary>
internal static class TestLanguage
{
    /// <summary>For a test where any saved name will do: the message as it is, placeholders and all.</summary>
    public static string Untranslated(UiText text) => text.Message;
}
