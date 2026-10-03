using Godot;

namespace NodeRunner.Ui.Lib;

internal static class UiPopupStyle
{
    public static UiTokens.Color SemanticToken(UiPopupType type) =>
        type switch
        {
            UiPopupType.Warn => UiTokens.Color.Halo,
            UiPopupType.Danger => UiTokens.Color.Danger,
            _ => UiTokens.Color.Accent,
        };

    /// <summary>The overline above a popup's title (#692): a normal popup has none.</summary>
    public static string Overline(UiPopupType type) =>
        type switch
        {
            UiPopupType.Warn => "Warning",
            UiPopupType.Danger => "Danger",
            _ => string.Empty,
        };

    public static Color SemanticColor(UiPopupType type, Control owner) =>
        UiThemeLookup.Color(owner, SemanticToken(type));

}
