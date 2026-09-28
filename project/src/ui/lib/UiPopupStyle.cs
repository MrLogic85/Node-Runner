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

    public static Color SemanticColor(UiPopupType type, Control owner) =>
        UiThemeLookup.Color(owner, SemanticToken(type));

}
