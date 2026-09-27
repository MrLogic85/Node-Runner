using Godot;

namespace NodeRunner.Ui.Lib;

internal static class UiPopupStyle
{
    public static UiCard.CardVariant CardKind(UiPopupType type) => type switch
    {
        UiPopupType.Warn => UiCard.CardVariant.Hint,
        UiPopupType.Danger => UiCard.CardVariant.Warning,
        _ => UiCard.CardVariant.Frame,
    };

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
