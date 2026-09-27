using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Shared helpers for compact component-library compositions.</summary>
internal static class UiFieldAndRows
{
    public static Label Label(
        string text,
        UiTokens.Typography style,
        UiTokens.Color color,
        HorizontalAlignment alignment = HorizontalAlignment.Left)
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
        UiThemeLookup.ApplyTextStyle(label, style, color);
        return label;
    }

    public static TextureRect Icon(UiIconId icon, UiIconSize size, Color tint) =>
        UiIcons.Create(icon, size, tint);

}
