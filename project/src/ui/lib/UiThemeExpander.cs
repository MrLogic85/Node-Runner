using Godot;
using GodotTheme = Godot.Theme;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Regenerates the palette-derived items of a Theme resource from its authored palette.
/// <para>
/// A theme file authors its <see cref="UiThemes.TokenType"/> colors, flags, and alphas. The project
/// theme (Neon) additionally authors everything that is the same in every palette: typography
/// variations, fonts, and spacing constants; other palette themes inherit those through Godot's
/// project-theme fallback. What this expander writes is only what combines a palette color with a
/// variation name, listed in <see cref="DerivedColors"/>. It clears every color of those types,
/// rewrites them, and leaves every other item in the file untouched.
/// Run it with <c>tools/ExpandThemes.tscn</c> after editing a palette color.
/// </para>
/// </summary>
public static class UiThemeExpander
{
    public readonly record struct ColorVariation(string Name, string BaseVariation, UiTokens.Color Color);
    /// <summary>A derived color item: <paramref name="Token"/> from the palette, scaled in alpha.</summary>
    public readonly record struct DerivedColor(string Type, string Name, UiTokens.Color Token, float AlphaScale = 1f);

    /// <summary>Colors each theme file must author; Transparent is the same everywhere.</summary>
    public static IReadOnlyList<UiTokens.Color> AuthoredColors { get; } =
        Enum.GetValues<UiTokens.Color>().Where(color => color != UiTokens.Color.Transparent).ToArray();

    /// <summary>Integer constants each theme file must author: flags and alphas.</summary>
    public static IReadOnlyList<string> AuthoredConstants { get; } =
        Enum.GetValues<UiTokens.Flag>().Select(UiTokens.Name)
            .Concat(Enum.GetValues<UiTokens.Alpha>().Select(UiTokens.Name))
            .ToArray();

    public static IReadOnlyList<ColorVariation> TextColorVariations { get; } =
        Enum.GetValues<UiTokens.Typography>()
            .SelectMany(typography => Enum.GetValues<UiTokens.Color>().Where(UiTokens.IsTextColor).Select(color => new ColorVariation(
                UiTokens.Variation(typography, color),
                UiTokens.Variation(typography),
                color)))
            .ToArray();

    /// <summary>
    /// UiButton's variation. Its native text is storage only (an internal UiLabel renders the
    /// caption), so every font color is transparent. Palette themes declare the base Button
    /// colors, and Godot searches each theme for all types before the next theme, so every palette
    /// theme carries this variation too.
    /// </summary>
    public const string ButtonVariationName = "UiButton";

    private static readonly string[] _buttonFontColors =
        ["font_color", "font_focus_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_disabled_color", "font_outline_color"];

    /// <summary>Rewrites the color-derived items in <paramref name="theme"/> from its authored palette.</summary>
    public static void Expand(GodotTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        Write(theme, ReadPalette(theme));
    }

    /// <summary>
    /// Makes <paramref name="target"/> a palette-only copy of <paramref name="source"/> with one flag
    /// changed. Everything palette-independent still comes from the project theme.
    /// </summary>
    public static void ExpandVariant(GodotTheme source, GodotTheme target, UiTokens.Flag flag, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        var palette = ReadPalette(source);
        palette.Constants[UiTokens.Name(flag)] = enabled ? 1 : 0;
        target.Clear();
        Write(target, palette);
    }

    private sealed record Palette(Dictionary<UiTokens.Color, Color> Colors, Dictionary<string, int> Constants);

    private static Palette ReadPalette(GodotTheme theme)
    {
        var colors = AuthoredColors.ToDictionary(color => color, color => Read(theme, color));
        colors[UiTokens.Color.Transparent] = Godot.Colors.Transparent;
        var constants = AuthoredConstants.ToDictionary(name => name, name =>
        {
            if (!theme.HasConstant(name, UiThemes.TokenType))
            {
                throw new InvalidOperationException($"{theme.ResourcePath} does not author {UiThemes.TokenType}/constants/{name}.");
            }
            return theme.GetConstant(name, UiThemes.TokenType);
        });
        return new Palette(colors, constants);
    }

    private static Color Read(GodotTheme theme, UiTokens.Color color)
    {
        var name = UiTokens.Name(color);
        if (!theme.HasColor(name, UiThemes.TokenType))
        {
            throw new InvalidOperationException($"{theme.ResourcePath} does not author {UiThemes.TokenType}/colors/{name}.");
        }
        return theme.GetColor(name, UiThemes.TokenType);
    }

    private static void Write(GodotTheme theme, Palette palette)
    {
        foreach (var (color, value) in palette.Colors)
        {
            theme.SetColor(UiTokens.Name(color), UiThemes.TokenType, value);
        }
        foreach (var (name, value) in palette.Constants)
        {
            theme.SetConstant(name, UiThemes.TokenType, value);
        }
        foreach (var type in DerivedColors.Select(item => item.Type).Distinct())
        {
            ClearColors(theme, type);
        }
        foreach (var (variation, baseType) in DerivedBaseTypes)
        {
            theme.SetTypeVariation(variation, baseType);
        }
        foreach (var item in DerivedColors)
        {
            theme.SetColor(item.Name, item.Type, palette.Colors[item.Token].ScaleAlpha(item.AlphaScale));
        }
    }

    /// <summary>Each variation this expander declares, with the type it chains onto.</summary>
    public static IReadOnlyDictionary<string, string> DerivedBaseTypes { get; } =
        TextColorVariations.Select(variation => KeyValuePair.Create(variation.Name, variation.BaseVariation))
            .Append(KeyValuePair.Create(ButtonVariationName, "Button"))
            .ToDictionary();

    /// <summary>
    /// Every color item this expander writes outside <see cref="UiThemes.TokenType"/>. Types listed
    /// here are cleared first, so this is the complete set of derived colors in a theme file.
    /// </summary>
    public static IReadOnlyList<DerivedColor> DerivedColors { get; } = [.. EnumerateDerivedColors()];

    private static IEnumerable<DerivedColor> EnumerateDerivedColors()
    {
        foreach (var variation in TextColorVariations)
        {
            yield return new(variation.Name, "font_color", variation.Color);
        }
        yield return new("Label", "font_color", UiTokens.Color.Ink);
        yield return new("Button", "font_color", UiTokens.Color.Ink);
        yield return new("Button", "font_hover_color", UiTokens.Color.Ink);
        yield return new("Button", "font_pressed_color", UiTokens.Color.Ink);
        yield return new("Button", "font_disabled_color", UiTokens.Color.Ink, 0.5f);
        yield return new("LineEdit", "font_color", UiTokens.Color.Ink);
        yield return new("LineEdit", "font_placeholder_color", UiTokens.Color.Muted);
        yield return new("CheckButton", "font_color", UiTokens.Color.Ink);
        yield return new("CheckBox", "font_color", UiTokens.Color.Ink);
        foreach (var state in _buttonFontColors)
        {
            yield return new(ButtonVariationName, state, UiTokens.Color.Transparent);
        }
    }

    private static void ClearColors(GodotTheme theme, string type)
    {
        foreach (var name in theme.GetColorList(type))
        {
            theme.ClearColor(name, type);
        }
    }
}
