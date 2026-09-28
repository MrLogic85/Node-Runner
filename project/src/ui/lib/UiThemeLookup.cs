using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Resolves typed UI token identifiers through a Control's inherited Theme. A missing item is
/// reported as an error in debug builds instead of silently resolving to Godot's fallback.
/// </summary>
public static class UiThemeLookup
{
    /// <summary>Theme constants store alphas as bytes because Godot rounds them to integers.</summary>
    private const float _alphaByteMax = 255f;
    private const float _lockedFillOpacity = 0.55f;
    private static readonly bool _reportMissing = OS.IsDebugBuild();

    public static Color Color(Control control, UiTokens.Color token)
    {
        ArgumentNullException.ThrowIfNull(control);
        var name = UiTokens.Name(token);
        if (_reportMissing && !control.HasThemeColor(name, UiThemes.TokenType))
        {
            ReportMissing("color", name, UiThemes.TokenType);
        }

        return control.GetThemeColor(name, UiThemes.TokenType);
    }

    public static bool Flag(Control control, UiTokens.Flag token)
    {
        ArgumentNullException.ThrowIfNull(control);
        var name = UiTokens.Name(token);
        if (_reportMissing && !control.HasThemeConstant(name, UiThemes.TokenType))
        {
            ReportMissing("constant", name, UiThemes.TokenType);
        }

        return control.GetThemeConstant(name, UiThemes.TokenType) != 0;
    }

    /// <summary>The palette's opacity for <paramref name="token"/>, authored as 0-255 because theme constants are integers.</summary>
    public static float Alpha(Control control, UiTokens.Alpha token)
    {
        ArgumentNullException.ThrowIfNull(control);
        var name = UiTokens.Name(token);
        if (_reportMissing && !control.HasThemeConstant(name, UiThemes.TokenType))
        {
            ReportMissing("constant", name, UiThemes.TokenType);
        }

        return control.GetThemeConstant(name, UiThemes.TokenType) / _alphaByteMax;
    }

    public static float Size(UiTokens.Size.Stroke stroke)
    {
        switch (stroke)
        {
            case UiTokens.Size.Stroke.Hair:
                return UiSize.Stroke.Hair;
            case UiTokens.Size.Stroke.Signal:
                return UiSize.Stroke.Signal;
            case UiTokens.Size.Stroke.Beam:
                return UiSize.Stroke.Beam;
            case UiTokens.Size.Stroke.ButtonSelected:
                return UiSize.Stroke.ButtonSelected;
            case UiTokens.Size.Stroke.SelectionHandle:
                return UiSize.Stroke.SelectionHandle;
            case UiTokens.Size.Stroke.InfoRing:
                return UiSize.Stroke.InfoRing;
            case UiTokens.Size.Stroke.Number:
                return UiSize.Stroke.Number;
            default:
                throw new ArgumentOutOfRangeException(nameof(stroke), stroke, null);
        }
    }

    public static int FontSize(Control control, UiTokens.Typography token)
    {
        ArgumentNullException.ThrowIfNull(control);
        var variation = UiTokens.Variation(token);
        if (_reportMissing && !control.HasThemeFontSize("font_size", variation))
        {
            ReportMissing("font size", "font_size", variation);
        }

        return control.GetThemeFontSize("font_size", variation);
    }

    private static void ReportMissing(string kind, string name, string type) =>
        GD.PushError($"Theme {kind} '{name}' is missing for type '{type}'.");

    /// <summary>
    /// Applies a typography's font through its theme variation. Letter case is part of the
    /// typography: a Label uses native <c>Uppercase</c>; Button has no such property, so its
    /// current text is upper-cased.
    /// </summary>
    public static void ApplyTypography(Control control, UiTokens.Typography token)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.ThemeTypeVariation = UiTokens.Variation(token);
        ApplyLetterCase(control, token);
    }

    /// <summary>
    /// Styles a Label by name only: the generated typography × text-color variation carries
    /// font and color, so a Theme swap restyles it without a refresh.
    /// </summary>
    public static void ApplyTextStyle(Label label, UiTokens.Typography typography, UiTokens.Color color)
    {
        ArgumentNullException.ThrowIfNull(label);
        label.ThemeTypeVariation = UiTokens.Variation(typography, color);
        label.RemoveThemeColorOverride("font_color");
        ApplyLetterCase(label, typography);
    }

    private static void ApplyLetterCase(Control control, UiTokens.Typography token)
    {
        bool uppercase = UiTokens.IsUppercase(token);
        if (control is Label label)
        {
            label.Uppercase = uppercase;
        }
        else if (uppercase && control is Button button)
        {
            button.Text = button.Text.ToUpperInvariant();
        }
    }

    public static StyleBoxFlat CreateStyleBox(
        Color background,
        Color borderColor,
        float? borderWidth = null,
        float? radius = null,
        float? horizontalPadding = null,
        float? verticalPadding = null)
    {
        var width = Mathf.RoundToInt(borderWidth ?? UiSize.Stroke.Hair);
        var cornerRadius = Mathf.RoundToInt(radius ?? UiSize.Radius.Medium);
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
            ContentMarginLeft = horizontalPadding ?? 0,
            ContentMarginTop = verticalPadding ?? 0,
            ContentMarginRight = horizontalPadding ?? 0,
            ContentMarginBottom = verticalPadding ?? 0,
        };
    }

    public static StyleBoxFlat CreateFrameStyleBox(
        Control control,
        UiSurfaceContracts.FrameVariant variant = UiSurfaceContracts.FrameVariant.Frame,
        UiSurfaceContracts.FrameSize size = UiSurfaceContracts.FrameSize.Default,
        bool glow = false)
    {
        var style = new StyleBoxFlat
        {
            BgColor = Color(control, UiTokens.Color.Panel),
            BorderColor = Color(control, UiTokens.Color.Edge),
            BorderWidthLeft = UiSize.Stroke.Hair,
            BorderWidthTop = UiSize.Stroke.Hair,
            BorderWidthRight = UiSize.Stroke.Hair,
            BorderWidthBottom = UiSize.Stroke.Hair,
            CornerRadiusTopLeft = UiSize.Radius.Large,
            CornerRadiusTopRight = UiSize.Radius.Large,
            CornerRadiusBottomLeft = UiSize.Radius.Large,
            CornerRadiusBottomRight = UiSize.Radius.Large,
        };

        switch (variant)
        {
            case UiSurfaceContracts.FrameVariant.Sel:
                style.BorderColor = Color(control, UiTokens.Color.Accent);
                SetBorderWidth(style, UiSize.Stroke.Signal);
                UiGlow.ApplyToControl(style, style.BorderColor, EffectsEnabled(control));
                break;
            case UiSurfaceContracts.FrameVariant.Pick:
                style.BorderColor = Color(control, UiTokens.Color.Halo);
                SetBorderWidth(style, UiSize.Stroke.Signal);
                break;
            case UiSurfaceContracts.FrameVariant.Lock:
                style.BorderColor = Color(control, UiTokens.Color.LineStrong);
                style.BgColor = style.BgColor.ScaleAlpha(_lockedFillOpacity);
                break;
            case UiSurfaceContracts.FrameVariant.Warn:
                style.BorderColor = Color(control, UiTokens.Color.Danger);
                break;
            case UiSurfaceContracts.FrameVariant.Hint:
                style.BorderColor = Color(control, UiTokens.Color.Halo);
                break;
            case UiSurfaceContracts.FrameVariant.Ok:
            case UiSurfaceContracts.FrameVariant.StageCard:
                style.BorderColor = Color(control, UiTokens.Color.Accent);
                if (variant == UiSurfaceContracts.FrameVariant.StageCard)
                {
                    UiGlow.ApplyToControl(style, style.BorderColor, EffectsEnabled(control));
                }
                break;
            case UiSurfaceContracts.FrameVariant.Raised:
                return CreateRaisedStyleBox(control);
        }

        if (glow)
        {
            UiGlow.ApplyToControl(style, style.BorderColor, EffectsEnabled(control));
        }

        var padding = size switch
        {
            UiSurfaceContracts.FrameSize.Snug => UiSize.Space.S2,
            UiSurfaceContracts.FrameSize.Tight => UiSize.Space.S1,
            UiSurfaceContracts.FrameSize.Roomy => UiSize.Space.S4,
            UiSurfaceContracts.FrameSize.Flush => 0,
            _ => UiSize.Space.S3,
        } + UiSize.Stroke.Hair;
        SetContentMargin(style, padding);
        return style;
    }

    public static StyleBoxFlat CreateRaisedStyleBox(
        Control control,
        UiSurfaceContracts.RaisedState state = UiSurfaceContracts.RaisedState.Rest)
    {
        var style = new StyleBoxFlat
        {
            BgColor = Color(control, UiTokens.Color.PanelRaised),
            BorderColor = Color(control, UiTokens.Color.LineStrong),
            BorderWidthLeft = UiSize.Stroke.Hair,
            BorderWidthTop = UiSize.Stroke.Hair,
            BorderWidthRight = UiSize.Stroke.Hair,
            BorderWidthBottom = UiSize.Stroke.Hair,
            CornerRadiusTopLeft = UiSize.Radius.Medium,
            CornerRadiusTopRight = UiSize.Radius.Medium,
            CornerRadiusBottomLeft = UiSize.Radius.Medium,
            CornerRadiusBottomRight = UiSize.Radius.Medium,
        };

        switch (state)
        {
            case UiSurfaceContracts.RaisedState.On:
            case UiSurfaceContracts.RaisedState.Primary:
                style.BgColor = Color(control, UiTokens.Color.Accent);
                style.BorderColor = style.BgColor;
                UiGlow.ApplyToControl(style, style.BgColor, EffectsEnabled(control));
                break;
            case UiSurfaceContracts.RaisedState.Lock:
            case UiSurfaceContracts.RaisedState.Off:
                style.BgColor = style.BgColor.ScaleAlpha(0.5f);
                style.BorderColor = style.BorderColor.ScaleAlpha(0.5f);
                break;
            case UiSurfaceContracts.RaisedState.Danger:
                style.BorderColor = Color(control, UiTokens.Color.Danger);
                break;
        }

        return style;
    }

    public static bool EffectsEnabled(Control control) => Flag(control, UiTokens.Flag.EffectsEnabled);



    private static void SetBorderWidth(StyleBoxFlat style, float value)
    {
        var width = Mathf.RoundToInt(value);
        style.BorderWidthLeft = width;
        style.BorderWidthTop = width;
        style.BorderWidthRight = width;
        style.BorderWidthBottom = width;
    }

    private static void SetContentMargin(StyleBoxFlat style, float value)
    {
        style.ContentMarginLeft = value;
        style.ContentMarginTop = value;
        style.ContentMarginRight = value;
        style.ContentMarginBottom = value;
    }
}
