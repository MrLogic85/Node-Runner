using Godot;
using GodotTheme = Godot.Theme;

namespace NodeRunner.Ui.Lib;

/// <summary>Selectable palettes: the neon lab skin and the paper skin.</summary>
public enum UiTokenType
{
    Neon,
    Paper,
}

/// <summary>
/// The Theme resources that are the source of truth for palette values. Neon is also the
/// project theme (<c>gui/theme/custom</c>), so every Control, including editor previews,
/// inherits it unless an ancestor assigns another one.
/// </summary>
public static class UiThemes
{
    private const float _alphaByteMax = 255f;

    public const string TokenType = "NodeRunner";

    public static string PathFor(UiTokenType type) => type switch
    {
        UiTokenType.Neon => "res://assets/themes/Neon.tres",
        UiTokenType.Paper => "res://assets/themes/Paper.tres",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static GodotTheme For(UiTokenType type) => GD.Load<GodotTheme>(PathFor(type));

    public static GodotTheme Neon => For(UiTokenType.Neon);

    public static GodotTheme Paper => For(UiTokenType.Paper);

    /// <summary>
    /// The theme <paramref name="node"/> inherits: the nearest one set on it or on a Control or
    /// Window above it, stopping at any other node, which cuts Godot's inheritance. Godot would
    /// fall through to outer themes for an item the nearest one lacks. None means the project
    /// theme, which applies anyway.
    /// </summary>
    public static GodotTheme? NearestOf(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        for (Node? at = node; at is Control or Window; at = at.GetParent())
        {
            var theme = at is Control control ? control.Theme : ((Window)at).Theme;
            if (theme is not null)
            {
                return theme;
            }
        }

        return null;
    }

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

    /// <summary>The palette's opacity for <paramref name="token"/>, authored as 0-255.</summary>
    public static float Alpha(GodotTheme theme, UiTokens.Alpha token)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return theme.GetConstant(UiTokens.Name(token), TokenType) / _alphaByteMax;
    }
}
