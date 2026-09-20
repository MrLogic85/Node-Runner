using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// App-agnostic design tokens consumed by reusable UI controls. Screens may
/// swap the complete set for paper or effects-lite presentation.
/// </summary>
public sealed class UiTokens
{
    private const string _barlowRegularPath = "res://assets/fonts/Barlow/Barlow-Regular.ttf";
    private const string _barlowMediumPath = "res://assets/fonts/Barlow/Barlow-Medium.ttf";
    private const string _chakraPetchSemiBoldPath = "res://assets/fonts/ChakraPetch/ChakraPetch-SemiBold.ttf";
    private const string _jetBrainsMonoRegularPath = "res://assets/fonts/JetBrainsMono/JetBrainsMono-Regular.ttf";
    private const string _jetBrainsMonoMediumPath = "res://assets/fonts/JetBrainsMono/JetBrainsMono-Medium.ttf";

    public enum FontFamily
    {
        Display,
        Body,
        Mono,
    }

    public readonly record struct TextStyle(
        FontFamily Family,
        float FontSize,
        float LineHeight,
        int FontWeight,
        float LetterSpacing = 0,
        bool Uppercase = false);

    public const float LogicalCanvasWidth = 640;
    public const float LogicalCanvasHeight = 360;

    public Color Background { get; init; }
    public Color Panel { get; init; }
    public Color PanelRaised { get; init; }
    public Color Line { get; init; }
    public Color LineStrong { get; init; }
    public Color Edge { get; init; }
    public Color Ink { get; init; }
    public Color Muted { get; init; }
    public Color Accent { get; init; }
    public Color AccentSoft { get; init; }
    public Color AccentGlow { get; init; }
    public Color Halo { get; init; }
    public Color OnAccent { get; init; }
    public Color Danger { get; init; }
    public float GlowRadius { get; init; } = 16;
    public float Space1 { get; init; } = 4;
    public float Space2 { get; init; } = 8;
    public float Space3 { get; init; } = 12;
    public float Space4 { get; init; } = 16;
    public float TouchTarget { get; init; } = 48;
    public float RadiusSmall { get; init; } = 4;
    public float RadiusMedium { get; init; } = 8;
    public float RadiusLarge { get; init; } = 14;
    public float RadiusPill { get; init; } = 999;
    public float StrokeHair { get; init; } = 1;
    public float StrokeSignal { get; init; } = 2;
    public float StrokeBeam { get; init; } = 3;
    public float TitleFontSize { get; init; } = 28;
    public float HeadingFontSize { get; init; } = 16;
    public float StageFontSize { get; init; } = 11;
    public float BodyFontSize { get; init; } = 13;
    public float LabelFontSize { get; init; } = 12;
    public float CaptionFontSize { get; init; } = 10;
    public float ReadoutLargeFontSize { get; init; } = 22;
    public float ReadoutFontSize { get; init; } = 13;
    public float ReadoutSmallFontSize { get; init; } = 10;
    public TextStyle TitleText { get; init; } = new(FontFamily.Display, 28, 32, 600);
    public TextStyle HeadingText { get; init; } = new(FontFamily.Display, 16, 20, 600);
    public TextStyle StageText { get; init; } = new(FontFamily.Display, 11, 14, 600, 0.06f, true);
    public TextStyle BodyText { get; init; } = new(FontFamily.Body, 13, 18, 400);
    public TextStyle LabelText { get; init; } = new(FontFamily.Body, 12, 16, 500, 0.04f, true);
    public TextStyle CaptionText { get; init; } = new(FontFamily.Body, 10, 13, 500);
    public TextStyle ReadoutLargeText { get; init; } = new(FontFamily.Mono, 22, 24, 500);
    public TextStyle ReadoutText { get; init; } = new(FontFamily.Mono, 13, 16, 500);
    public TextStyle ReadoutSmallText { get; init; } = new(FontFamily.Mono, 10, 13, 400);
    public bool EffectsEnabled { get; init; } = true;

    public static UiTokens Neon { get; } = new()
    {
        Background = Rgb(0x07, 0x0b, 0x14),
        Panel = Rgb(0x0d, 0x14, 0x24),
        PanelRaised = Rgb(0x13, 0x1c, 0x31),
        Line = Rgb(0x23, 0x30, 0x4d),
        LineStrong = Rgb(0x55, 0x73, 0xa6),
        Edge = Rgb(0x19, 0xf0, 0xff, 0x59),
        Ink = Rgb(0xe6, 0xf1, 0xff),
        Muted = Rgb(0x8f, 0xa3, 0xc4),
        Accent = Rgb(0x19, 0xf0, 0xff),
        AccentSoft = Rgb(0x19, 0xf0, 0xff, 0x1f),
        AccentGlow = Rgb(0x19, 0xf0, 0xff, 0x40),
        Halo = Rgb(0xff, 0xb3, 0x47),
        OnAccent = Rgb(0x04, 0x12, 0x1a),
        Danger = Rgb(0xff, 0x6b, 0x87),
    };

    public static UiTokens Paper { get; } = new()
    {
        Background = Rgb(0xf4, 0xf1, 0xea),
        Panel = Rgb(0xff, 0xff, 0xff),
        PanelRaised = Rgb(0xe9, 0xe5, 0xdb),
        Line = Rgb(0xcf, 0xc8, 0xb8),
        LineStrong = Rgb(0x7a, 0x74, 0x66),
        Edge = Rgb(0xcf, 0xc8, 0xb8),
        Ink = Rgb(0x1b, 0x1a, 0x17),
        Muted = Rgb(0x5a, 0x56, 0x48),
        Accent = Rgb(0x00, 0x6d, 0x77),
        AccentSoft = Rgb(0x00, 0x6d, 0x77, 0x1a),
        AccentGlow = Rgb(0x00, 0x6d, 0x77, 0x00),
        Halo = Rgb(0xb4, 0x5f, 0x00),
        OnAccent = Rgb(0xff, 0xff, 0xff),
        Danger = Rgb(0xb3, 0x26, 0x1e),
        GlowRadius = 0,
        EffectsEnabled = false,
    };

    public UiTokens WithEffects(bool enabled) => new()
    {
        Background = Background,
        Panel = Panel,
        PanelRaised = PanelRaised,
        Line = Line,
        LineStrong = LineStrong,
        Edge = Edge,
        Ink = Ink,
        Muted = Muted,
        Accent = Accent,
        AccentSoft = AccentSoft,
        AccentGlow = enabled ? AccentGlow : Colors.Transparent,
        Halo = Halo,
        OnAccent = OnAccent,
        Danger = Danger,
        GlowRadius = enabled ? GlowRadius : 0,
        Space1 = Space1,
        Space2 = Space2,
        Space3 = Space3,
        Space4 = Space4,
        TouchTarget = TouchTarget,
        RadiusSmall = RadiusSmall,
        RadiusMedium = RadiusMedium,
        RadiusLarge = RadiusLarge,
        RadiusPill = RadiusPill,
        StrokeHair = StrokeHair,
        StrokeSignal = StrokeSignal,
        StrokeBeam = StrokeBeam,
        TitleFontSize = TitleFontSize,
        HeadingFontSize = HeadingFontSize,
        StageFontSize = StageFontSize,
        BodyFontSize = BodyFontSize,
        LabelFontSize = LabelFontSize,
        CaptionFontSize = CaptionFontSize,
        ReadoutLargeFontSize = ReadoutLargeFontSize,
        ReadoutFontSize = ReadoutFontSize,
        ReadoutSmallFontSize = ReadoutSmallFontSize,
        TitleText = TitleText,
        HeadingText = HeadingText,
        StageText = StageText,
        BodyText = BodyText,
        LabelText = LabelText,
        CaptionText = CaptionText,
        ReadoutLargeText = ReadoutLargeText,
        ReadoutText = ReadoutText,
        ReadoutSmallText = ReadoutSmallText,
        EffectsEnabled = enabled,
    };

    public void ApplyTextStyle(Control control, TextStyle style)
    {
        control.AddThemeFontSizeOverride("font_size", (int)style.FontSize);
        control.AddThemeConstantOverride("line_spacing", (int)Math.Max(0, style.LineHeight - style.FontSize));
        if (TryLoadFont(style, out var font))
        {
            control.AddThemeFontOverride("font", font);
        }

        control.SetMeta("ui_font_family", style.Family.ToString());
        control.SetMeta("ui_line_height", style.LineHeight);
        control.SetMeta("ui_font_weight", style.FontWeight);
        control.SetMeta("ui_letter_spacing", style.LetterSpacing);
        control.SetMeta("ui_uppercase", style.Uppercase);
    }

    public void ApplyTextStyle(Label label, TextStyle style)
    {
        ApplyTextStyle((Control)label, style);
        if (style.Uppercase)
        {
            label.Text = label.Text.ToUpperInvariant();
        }
    }

    public void ApplyTextStyle(Button button, TextStyle style)
    {
        ApplyTextStyle((Control)button, style);
        if (style.Uppercase)
        {
            button.Text = button.Text.ToUpperInvariant();
        }
    }

    private static bool TryLoadFont(TextStyle style, out Font font)
    {
        font = GD.Load<Font>(FontPathFor(style));
        return font is not null;
    }

    private static string FontPathFor(TextStyle style) =>
        style.Family switch
        {
            FontFamily.Display => _chakraPetchSemiBoldPath,
            FontFamily.Mono => style.FontWeight >= 500 ? _jetBrainsMonoMediumPath : _jetBrainsMonoRegularPath,
            _ => style.FontWeight >= 500 ? _barlowMediumPath : _barlowRegularPath,
        };

    public StyleBoxFlat PanelStyle(
        bool raised = false,
        Color? borderColor = null,
        float? borderWidth = null,
        float? radius = null)
    {
        var width = (int)(borderWidth ?? StrokeHair);
        var cornerRadius = (int)(radius ?? RadiusLarge);
        return new StyleBoxFlat
        {
            BgColor = raised ? PanelRaised : Panel,
            BorderColor = borderColor ?? Edge,
            BorderWidthLeft = width,
            BorderWidthTop = width,
            BorderWidthRight = width,
            BorderWidthBottom = width,
            CornerRadiusTopLeft = cornerRadius,
            CornerRadiusTopRight = cornerRadius,
            CornerRadiusBottomLeft = cornerRadius,
            CornerRadiusBottomRight = cornerRadius,
        };
    }

    public StyleBoxFlat ControlStyle(
        Color background,
        Color borderColor,
        float? borderWidth = null,
        float? radius = null,
        bool glow = false)
    {
        var width = (int)(borderWidth ?? StrokeHair);
        var cornerRadius = (int)(radius ?? RadiusMedium);
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = borderColor,
            BorderWidthLeft = width,
            BorderWidthTop = width,
            BorderWidthRight = width,
            BorderWidthBottom = width,
            CornerRadiusTopLeft = cornerRadius,
            CornerRadiusTopRight = cornerRadius,
            CornerRadiusBottomLeft = cornerRadius,
            CornerRadiusBottomRight = cornerRadius,
            ShadowColor = glow && EffectsEnabled ? AccentGlow : Colors.Transparent,
            ShadowSize = glow && EffectsEnabled ? (int)GlowRadius : 0,
        };
    }

    public static Color WithAlpha(Color color, float alpha) =>
        new(color.R, color.G, color.B, alpha);

    public static Color MultiplyAlpha(Color color, float multiplier) =>
        new(color.R, color.G, color.B, color.A * multiplier);

    private static Color Rgb(int red, int green, int blue, int alpha = 0xff) =>
        new(red / 255f, green / 255f, blue / 255f, alpha / 255f);
}
