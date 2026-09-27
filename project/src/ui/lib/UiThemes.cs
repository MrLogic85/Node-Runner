using Godot;
using GodotTheme = Godot.Theme;

namespace NodeRunner.Ui.Lib;

/// <summary>Selectable palettes: the neon lab skin, the paper skin, and neon with Effects Lite.</summary>
public enum UiTokenType
{
    Neon,
    Paper,
    Light,
}

/// <summary>
/// The Theme resources that are the source of truth for palette values. Neon is also the
/// project theme (<c>gui/theme/custom</c>), so every Control, including editor previews,
/// inherits it unless an ancestor assigns another one.
/// </summary>
public static class UiThemes
{
    public const string TokenType = "NodeRunner";

    public static string PathFor(UiTokenType type) => type switch
    {
        UiTokenType.Neon => "res://assets/themes/Neon.tres",
        UiTokenType.Paper => "res://assets/themes/Paper.tres",
        UiTokenType.Light => "res://assets/themes/NeonLite.tres",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static GodotTheme For(UiTokenType type) => GD.Load<GodotTheme>(PathFor(type));

    public static GodotTheme Neon => For(UiTokenType.Neon);

    public static GodotTheme Paper => For(UiTokenType.Paper);

    public static Color Color(GodotTheme theme, UiTokens.Color token)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return theme.GetColor(UiTokens.Name(token), TokenType);
    }

    public static bool Flag(GodotTheme theme, UiTokens.Flag token)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return theme.GetConstant(UiTokens.Name(token), TokenType) != 0;
    }
}
