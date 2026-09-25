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
    private const string _barlowSemiBoldPath = "res://assets/fonts/Barlow/Barlow-SemiBold.ttf";
    private const string _chakraPetchSemiBoldPath = "res://assets/fonts/ChakraPetch/ChakraPetch-SemiBold.ttf";
    private const string _chakraPetchBoldPath = "res://assets/fonts/ChakraPetch/ChakraPetch-Bold.ttf";
    private const string _jetBrainsMonoRegularPath = "res://assets/fonts/JetBrainsMono/JetBrainsMono-Regular.ttf";
    private const string _jetBrainsMonoMediumPath = "res://assets/fonts/JetBrainsMono/JetBrainsMono-Medium.ttf";
    private const string _jetBrainsMonoSemiBoldPath = "res://assets/fonts/JetBrainsMono/JetBrainsMono-SemiBold.ttf";

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
    public Color Scrim { get; init; }
    public Color Output { get; init; }
    public float GlowRadius { get; init; } = 16;
    public float Space1 { get; init; } = 4;
    public float Space2 { get; init; } = 8;
    public float Space3 { get; init; } = 12;
    public float Space4 { get; init; } = 16;
    public float Space5 { get; init; } = 24;
    public float ControlExtraSmall { get; init; } = 24;
    public float ControlSmall { get; init; } = 32;
    public float ControlHeight { get; init; } = 40;
    public float TouchTarget { get; init; } = 48;
    public float BadgeMinimumSize { get; init; } = 16;
    public float BadgeOffset { get; init; } = 4;
    public float ButtonSelectedStroke { get; init; } = 2;
    public float NumberDiameter { get; init; } = 16;
    public float NumberStrokeWidth { get; init; } = 1.5f;
    public float IconSmall { get; init; } = 12;
    public float Icon { get; init; } = 16;
    public float IconLarge { get; init; } = 20;
    public float IconExtraLarge { get; init; } = 24;
    public float RailWidth { get; init; } = 56;
    public float SidePanelWidth { get; init; } = 176;
    public float MenuWidth { get; init; } = 200;
    public float DialogWidth { get; init; } = 300;
    public float BrainWidth { get; init; } = 460;
    public float CardWidth { get; init; } = 326;
    public float TileWidth { get; init; } = 156;
    public float WellWidth { get; init; } = 250;
    public float SheetWidth { get; init; } = 720;
    public float SheetWideWidth { get; init; } = 880;
    public float ScreenBodyHeight { get; init; } = 312;
    public float StageHeight { get; init; } = 170;
    public float ThumbnailHeight { get; init; } = 100;
    public float ColumnExtraSmallWidth { get; init; } = 40;
    public float ColumnSmallWidth { get; init; } = 52;
    public float ColumnMediumWidth { get; init; } = 76;
    public float ColumnLargeWidth { get; init; } = 96;
    public float ColumnExtraLargeWidth { get; init; } = 128;
    public float RadiusSmall { get; init; } = 4;
    public float RadiusMedium { get; init; } = 8;
    public float RadiusLarge { get; init; } = 12;
    public float RadiusPill { get; init; } = 999;
    public float StrokeHair { get; init; } = 1;
    public float StrokeSignal { get; init; } = 2;
    public float StrokeBeam { get; init; } = 3;
    public float SliderThumbDiameter { get; init; } = 18;
    public float SliderTrackWidth { get; init; } = 4;
    public float SliderMarkerHeight { get; init; } = 16;
    public float SliderStepTickHeight { get; init; } = 10;
    public float SliderDisabledDashLength { get; init; } = 4;
    public float SliderSteppedHeight { get; init; } = 60;
    public TextStyle TitleText { get; init; } = new(FontFamily.Display, 28, 32, 700);
    public TextStyle HeadingText { get; init; } = new(FontFamily.Display, 16, 20, 600);
    public TextStyle SubheadingText { get; init; } = new(FontFamily.Display, 14, 18, 600);
    public TextStyle StageText { get; init; } = new(FontFamily.Display, 11, 14, 600, 0.06f, true);
    public TextStyle BodyText { get; init; } = new(FontFamily.Body, 13, 18, 400);
    public TextStyle BodyStrongText { get; init; } = new(FontFamily.Body, 13, 18, 600);
    public TextStyle SmallText { get; init; } = new(FontFamily.Body, 12, 16, 400);
    public TextStyle SmallStrongText { get; init; } = new(FontFamily.Body, 12, 16, 600);
    public TextStyle LabelText { get; init; } = new(FontFamily.Body, 12, 16, 600, 0.04f, true);
    public TextStyle NoteText { get; init; } = new(FontFamily.Body, 11, 14, 400);
    public TextStyle NoteStrongText { get; init; } = new(FontFamily.Body, 11, 14, 600);
    public TextStyle CaptionText { get; init; } = new(FontFamily.Body, 10, 13, 500);
    public TextStyle OverlineText { get; init; } = new(FontFamily.Body, 10, 13, 600, 0.06f, true);
    public TextStyle ReadoutLargeText { get; init; } = new(FontFamily.Mono, 22, 24, 600);
    public TextStyle ReadoutText { get; init; } = new(FontFamily.Mono, 13, 16, 500);
    public TextStyle ReadoutMediumText { get; init; } = new(FontFamily.Mono, 12, 16, 500);
    public TextStyle ReadoutSmallText { get; init; } = new(FontFamily.Mono, 10, 13, 500);
    public bool EffectsEnabled { get; init; } = true;

    public static UiTokens Neon { get; } = new()
    {
        Background = Rgb(0x07, 0x0b, 0x14),
        Panel = Rgb(0x0d, 0x14, 0x24),
        PanelRaised = Rgb(0x13, 0x1c, 0x31),
        Line = Rgb(0x23, 0x30, 0x4d),
        LineStrong = Rgb(0x55, 0x73, 0xa6),
        Edge = Rgb(0x1a, 0x7f, 0x79),
        Ink = Rgb(0xe6, 0xf1, 0xff),
        Muted = Rgb(0x8f, 0xa3, 0xc4),
        Accent = Rgb(0x19, 0xf0, 0xff),
        AccentSoft = Rgb(0x19, 0xf0, 0xff, 0x1f),
        AccentGlow = Rgb(0x19, 0xf0, 0xff, 0x40),
        Halo = Rgb(0xff, 0xb3, 0x47),
        OnAccent = Rgb(0x04, 0x12, 0x1a),
        Danger = Rgb(0xff, 0x6b, 0x87),
        Scrim = Rgb(0x04, 0x08, 0x10, 0xbd),
        Output = Rgb(0xff, 0xe1, 0x4d),
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
        Scrim = Rgb(0x1b, 0x1a, 0x17, 0x73),
        Output = Rgb(0x7a, 0x5c, 0x00),
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
        Scrim = Scrim,
        Output = Output,
        GlowRadius = enabled ? GlowRadius : 0,
        Space1 = Space1,
        Space2 = Space2,
        Space3 = Space3,
        Space4 = Space4,
        Space5 = Space5,
        ControlExtraSmall = ControlExtraSmall,
        ControlSmall = ControlSmall,
        ControlHeight = ControlHeight,
        TouchTarget = TouchTarget,
        BadgeMinimumSize = BadgeMinimumSize,
        BadgeOffset = BadgeOffset,
        ButtonSelectedStroke = ButtonSelectedStroke,
        NumberDiameter = NumberDiameter,
        NumberStrokeWidth = NumberStrokeWidth,
        IconSmall = IconSmall,
        Icon = Icon,
        IconLarge = IconLarge,
        IconExtraLarge = IconExtraLarge,
        RailWidth = RailWidth,
        SidePanelWidth = SidePanelWidth,
        MenuWidth = MenuWidth,
        DialogWidth = DialogWidth,
        BrainWidth = BrainWidth,
        CardWidth = CardWidth,
        TileWidth = TileWidth,
        WellWidth = WellWidth,
        SheetWidth = SheetWidth,
        SheetWideWidth = SheetWideWidth,
        ScreenBodyHeight = ScreenBodyHeight,
        StageHeight = StageHeight,
        ThumbnailHeight = ThumbnailHeight,
        ColumnExtraSmallWidth = ColumnExtraSmallWidth,
        ColumnSmallWidth = ColumnSmallWidth,
        ColumnMediumWidth = ColumnMediumWidth,
        ColumnLargeWidth = ColumnLargeWidth,
        ColumnExtraLargeWidth = ColumnExtraLargeWidth,
        RadiusSmall = RadiusSmall,
        RadiusMedium = RadiusMedium,
        RadiusLarge = RadiusLarge,
        RadiusPill = RadiusPill,
        StrokeHair = StrokeHair,
        StrokeSignal = StrokeSignal,
        StrokeBeam = StrokeBeam,
        SliderThumbDiameter = SliderThumbDiameter,
        SliderTrackWidth = SliderTrackWidth,
        SliderMarkerHeight = SliderMarkerHeight,
        SliderStepTickHeight = SliderStepTickHeight,
        SliderDisabledDashLength = SliderDisabledDashLength,
        SliderSteppedHeight = SliderSteppedHeight,
        TitleText = TitleText,
        HeadingText = HeadingText,
        SubheadingText = SubheadingText,
        StageText = StageText,
        BodyText = BodyText,
        BodyStrongText = BodyStrongText,
        SmallText = SmallText,
        SmallStrongText = SmallStrongText,
        LabelText = LabelText,
        NoteText = NoteText,
        NoteStrongText = NoteStrongText,
        CaptionText = CaptionText,
        OverlineText = OverlineText,
        ReadoutLargeText = ReadoutLargeText,
        ReadoutText = ReadoutText,
        ReadoutMediumText = ReadoutMediumText,
        ReadoutSmallText = ReadoutSmallText,
        EffectsEnabled = enabled,
    };

    public void ApplyTextStyle(Control control, TextStyle style)
    {
        control.AddThemeFontSizeOverride("font_size", (int)style.FontSize);
        if (CreateTextFont(style) is { } font)
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

    public static FontVariation? CreateTextFont(TextStyle style)
    {
        var font = GD.Load<Font>(FontPathFor(style));
        if (font is null)
        {
            GD.PushError($"Could not load typography font: {FontPathFor(style)}");
            return null;
        }
        var adjustment = style.LineHeight - font.GetHeight((int)style.FontSize);
        var spacingTop = (int)Math.Floor(adjustment / 2);
        return new FontVariation
        {
            BaseFont = font,
            SpacingTop = spacingTop,
            SpacingBottom = (int)Math.Round(adjustment) - spacingTop,
            SpacingGlyph = style.LetterSpacing > 0
                ? Math.Max(1, (int)Math.Round(style.FontSize * style.LetterSpacing))
                : 0,
        };
    }

    public static string FontPathFor(TextStyle style) =>
        style.Family switch
        {
            FontFamily.Display => style.FontWeight >= 700 ? _chakraPetchBoldPath : _chakraPetchSemiBoldPath,
            FontFamily.Mono => style.FontWeight >= 600
                ? _jetBrainsMonoSemiBoldPath
                : style.FontWeight >= 500
                    ? _jetBrainsMonoMediumPath
                    : _jetBrainsMonoRegularPath,
            _ => style.FontWeight >= 600
                ? _barlowSemiBoldPath
                : style.FontWeight >= 500
                    ? _barlowMediumPath
                    : _barlowRegularPath,
        };

    /// <summary>Creates the canonical Frame surface for panels and cards.</summary>
    public StyleBoxFlat FrameStyle(
        UiSurfaceContracts.FrameVariant variant = UiSurfaceContracts.FrameVariant.Frame,
        UiSurfaceContracts.FrameSize size = UiSurfaceContracts.FrameSize.Default,
        bool glow = false)
    {
        var style = new StyleBoxFlat
        {
            BgColor = Panel,
            BorderColor = Edge,
            BorderWidthLeft = (int)StrokeHair,
            BorderWidthTop = (int)StrokeHair,
            BorderWidthRight = (int)StrokeHair,
            BorderWidthBottom = (int)StrokeHair,
            CornerRadiusTopLeft = (int)RadiusLarge,
            CornerRadiusTopRight = (int)RadiusLarge,
            CornerRadiusBottomLeft = (int)RadiusLarge,
            CornerRadiusBottomRight = (int)RadiusLarge,
        };

        switch (variant)
        {
            case UiSurfaceContracts.FrameVariant.Sel:
                style.BorderColor = Accent;
                SetBorderWidth(style, StrokeSignal);
                AddGlow(style, Accent);
                break;
            case UiSurfaceContracts.FrameVariant.Pick:
                style.BorderColor = Halo;
                SetBorderWidth(style, StrokeSignal);
                break;
            case UiSurfaceContracts.FrameVariant.Lock:
                style.BorderColor = LineStrong;
                SetBorderWidth(style, StrokeHair);
                style.BgColor = MultiplyAlpha(style.BgColor, 0.55f);
                break;
            case UiSurfaceContracts.FrameVariant.Warn:
                style.BorderColor = Danger;
                break;
            case UiSurfaceContracts.FrameVariant.Hint:
                style.BorderColor = Halo;
                break;
            case UiSurfaceContracts.FrameVariant.Ok:
                style.BorderColor = Accent;
                break;
            case UiSurfaceContracts.FrameVariant.Raised:
                return RaisedStyle();
            case UiSurfaceContracts.FrameVariant.StageCard:
                style.BorderColor = Accent;
                AddGlow(style, Accent);
                break;
        }

        if (glow)
        {
            AddGlow(style, style.BorderColor);
        }

        var padding = size switch
        {
            UiSurfaceContracts.FrameSize.Snug => Space2,
            UiSurfaceContracts.FrameSize.Tight => Space1,
            UiSurfaceContracts.FrameSize.Roomy => Space4,
            UiSurfaceContracts.FrameSize.Flush => 0,
            _ => Space3,
        } + StrokeHair;
        SetContentMargin(style, padding);
        return style;
    }

    /// <summary>Creates the canonical Raised surface for pressable or editable controls.</summary>
    public StyleBoxFlat RaisedStyle(
        UiSurfaceContracts.RaisedState state = UiSurfaceContracts.RaisedState.Rest)
    {
        var style = new StyleBoxFlat
        {
            BgColor = PanelRaised,
            BorderColor = LineStrong,
            BorderWidthLeft = (int)StrokeHair,
            BorderWidthTop = (int)StrokeHair,
            BorderWidthRight = (int)StrokeHair,
            BorderWidthBottom = (int)StrokeHair,
            CornerRadiusTopLeft = (int)RadiusMedium,
            CornerRadiusTopRight = (int)RadiusMedium,
            CornerRadiusBottomLeft = (int)RadiusMedium,
            CornerRadiusBottomRight = (int)RadiusMedium,
        };

        switch (state)
        {
            case UiSurfaceContracts.RaisedState.On:
            case UiSurfaceContracts.RaisedState.Primary:
                style.BgColor = Accent;
                style.BorderColor = Accent;
                UiGlow.ApplyToControl(style, Accent, EffectsEnabled);
                break;
            case UiSurfaceContracts.RaisedState.Lock:
            case UiSurfaceContracts.RaisedState.Off:
                style.BgColor = MultiplyAlpha(style.BgColor, 0.5f);
                style.BorderColor = MultiplyAlpha(style.BorderColor, 0.5f);
                break;
            case UiSurfaceContracts.RaisedState.Danger:
                style.BorderColor = Danger;
                break;
        }

        return style;
    }

    /// <summary>
    /// Compatibility adapter for controls that still call the old panel helper.
    /// New surfaces must use <see cref="FrameStyle"/> or <see cref="RaisedStyle"/>.
    /// </summary>
    [Obsolete("Use FrameStyle or RaisedStyle; the bool overload conflates two surfaces.")]
    public StyleBoxFlat PanelStyle(
        bool raised = false,
        Color? borderColor = null,
        float? borderWidth = null,
        float? radius = null)
    {
        var style = raised ? RaisedStyle() : FrameStyle();
        if (borderColor.HasValue)
        {
            style.BorderColor = borderColor.Value;
        }

        if (borderWidth.HasValue)
        {
            SetBorderWidth(style, borderWidth.Value);
        }

        if (radius.HasValue)
        {
            SetCornerRadius(style, radius.Value);
        }

        return style;
    }

    private void AddGlow(StyleBoxFlat style, Color color)
    {
        UiGlow.ApplyToControl(style, color, EffectsEnabled);
    }

    private static void SetContentMargin(StyleBoxFlat style, float value)
    {
        style.ContentMarginLeft = value;
        style.ContentMarginTop = value;
        style.ContentMarginRight = value;
        style.ContentMarginBottom = value;
    }

    private static void SetBorderWidth(StyleBoxFlat style, float value)
    {
        var width = (int)value;
        style.BorderWidthLeft = width;
        style.BorderWidthTop = width;
        style.BorderWidthRight = width;
        style.BorderWidthBottom = width;
    }

    private static void SetCornerRadius(StyleBoxFlat style, float value)
    {
        var radius = (int)value;
        style.CornerRadiusTopLeft = radius;
        style.CornerRadiusTopRight = radius;
        style.CornerRadiusBottomLeft = radius;
        style.CornerRadiusBottomRight = radius;
    }

    public StyleBoxFlat ControlStyle(
        Color background,
        Color borderColor,
        float? borderWidth = null,
        float? radius = null,
        bool glow = false,
        float? horizontalPadding = null,
        float? verticalPadding = null)
    {
        var width = (int)(borderWidth ?? StrokeHair);
        var cornerRadius = (int)(radius ?? RadiusMedium);
        var style = new StyleBoxFlat
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
            ContentMarginLeft = horizontalPadding ?? 0,
            ContentMarginTop = verticalPadding ?? 0,
            ContentMarginRight = horizontalPadding ?? 0,
            ContentMarginBottom = verticalPadding ?? 0,
        };
        if (glow)
        {
            UiGlow.ApplyToControl(style, Accent, EffectsEnabled);
        }

        return style;
    }

    public static Color WithAlpha(Color color, float alpha) =>
        new(color.R, color.G, color.B, alpha);

    public static Color MultiplyAlpha(Color color, float multiplier) =>
        new(color.R, color.G, color.B, color.A * multiplier);

    private static Color Rgb(int red, int green, int blue, int alpha = 0xff) =>
        new(red / 255f, green / 255f, blue / 255f, alpha / 255f);
}
