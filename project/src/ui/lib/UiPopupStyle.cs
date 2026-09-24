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

    public static Color SemanticColor(UiPopupType type, UiTokens tokens) =>
        type switch { UiPopupType.Warn => tokens.Halo, UiPopupType.Danger => tokens.Danger, _ => tokens.Accent };

}
