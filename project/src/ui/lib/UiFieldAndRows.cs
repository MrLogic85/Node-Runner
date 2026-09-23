using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Shared helpers for compact component-library compositions.</summary>
internal static class UiFieldAndRows
{
    public static Label Label(string text, UiTokens tokens, UiTokens.TextStyle style, Color color, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var label = new Label
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = alignment,
            AutowrapMode = TextServer.AutowrapMode.Off,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        tokens.ApplyTextStyle(label, style);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    public static TextureRect Icon(UiIconId icon, UiIconSize size, Color tint) =>
        UiIcons.Create(icon, size, tint);

    public static StyleBoxFlat DashedLike(UiTokens tokens, Color border, bool raised = true) =>
        tokens.ControlStyle(
            raised ? tokens.PanelRaised : tokens.Panel,
            border,
            0,
            tokens.RadiusMedium,
            horizontalPadding: tokens.Space2,
            verticalPadding: tokens.Space1);
}
