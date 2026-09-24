using Godot;

namespace NodeRunner.Ui.Lib;

internal static class UiPopupStyle
{
    public static UiCard Card(UiPopupType type, UiTokens tokens) => new()
    {
        Tokens = tokens,
        Kind = CardKind(type),
    };

    public static UiCard.CardVariant CardKind(UiPopupType type) => type switch
    {
        UiPopupType.Warn => UiCard.CardVariant.Hint,
        UiPopupType.Danger => UiCard.CardVariant.Warning,
        _ => UiCard.CardVariant.Frame,
    };

    public static Color SemanticColor(UiPopupType type, UiTokens tokens) =>
        type switch { UiPopupType.Warn => tokens.Halo, UiPopupType.Danger => tokens.Danger, _ => tokens.Accent };

    public static Control Heading(UiPopupType type, string title, UiTokens tokens)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", (int)tokens.Space2);
        var color = SemanticColor(type, tokens);
        row.AddChild(UiIcons.Create(type == UiPopupType.Default ? UiIconId.Model : UiIconId.Warn, UiIconSize.Large, color));
        var titles = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        titles.AddChild(Text(type.ToString(), tokens.OverlineText, tokens, color));
        titles.AddChild(Text(title, tokens.SubheadingText, tokens));
        row.AddChild(titles);
        return row;
    }

    public static Label Text(string text, UiTokens.TextStyle style, UiTokens tokens, Color? color = null)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        tokens.ApplyTextStyle(label, style);
        label.AddThemeColorOverride("font_color", color ?? tokens.Ink);
        return label;
    }
}
