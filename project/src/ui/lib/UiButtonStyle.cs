using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Semantic variants of the canonical reusable button.</summary>
public enum UiButtonKind
{
    Primary,
    Secondary,
    Tertiary,
    Flat,
}

/// <summary>Visual layouts supported by the canonical reusable button.</summary>
public enum UiButtonContentLayout
{
    // Keep existing scene values stable when adding the compact row option.
    Row = 0,
    RowCompact = 2,
    Stacked = 1,
}

/// <summary>
/// Composes a button's semantic colours independently from its layout and state.
/// A rest glow is optional; selected glow is always derived from
/// <see cref="SelectedColor"/>.
/// </summary>
public readonly record struct UiButtonStyle(
    UiButtonKind Kind,
    UiTokens.Color BackgroundColor,
    UiTokens.Color BorderColor,
    UiTokens.Color ContentColor,
    UiTokens.Color SelectedColor,
    UiTokens.Color? RestGlowColor = null)
{
    public static UiButtonStyle Primary { get; } = new(
        UiButtonKind.Primary,
        UiTokens.Color.Accent,
        UiTokens.Color.Accent,
        UiTokens.Color.OnAccent,
        UiTokens.Color.Halo,
        UiTokens.Color.Accent);

    public static UiButtonStyle Secondary { get; } = new(
        UiButtonKind.Secondary,
        UiTokens.Color.PanelRaised,
        UiTokens.Color.LineStrong,
        UiTokens.Color.Ink,
        UiTokens.Color.Accent);

    public static UiButtonStyle Tertiary { get; } = new(
        UiButtonKind.Tertiary,
        UiTokens.Color.PanelRaised,
        UiTokens.Color.Danger,
        UiTokens.Color.Danger,
        UiTokens.Color.Danger);

    public static UiButtonStyle Flat { get; } = new(
        UiButtonKind.Flat,
        UiTokens.Color.Transparent,
        UiTokens.Color.Transparent,
        UiTokens.Color.Ink,
        UiTokens.Color.Ink);

    public static UiButtonStyle For(UiButtonKind kind) => kind switch
    {
        UiButtonKind.Primary => Primary,
        UiButtonKind.Secondary => Secondary,
        UiButtonKind.Tertiary => Tertiary,
        UiButtonKind.Flat => Flat,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public UiResolvedButtonStyle Resolve(Control control) =>
        Resolve(color => ResolveColor(control, color));

    public UiResolvedButtonStyle Resolve(Func<UiTokens.Color, Color> resolveColor) => new(
        resolveColor(BackgroundColor),
        resolveColor(BorderColor),
        resolveColor(ContentColor),
        resolveColor(SelectedColor),
        RestGlowColor is { } restGlow ? resolveColor(restGlow) : null);

    public Color BorderFor(Control control, bool selected) =>
        BorderFor(color => ResolveColor(control, color), selected);

    public Color BorderFor(Func<UiTokens.Color, Color> resolveColor, bool selected) =>
        selected ? Resolve(resolveColor).Selected : Resolve(resolveColor).Border;

    public Color? GlowBaseFor(Control control, bool selected) =>
        GlowBaseFor(color => ResolveColor(control, color), selected);

    public Color? GlowBaseFor(Func<UiTokens.Color, Color> resolveColor, bool selected) =>
        selected ? Resolve(resolveColor).Selected : Resolve(resolveColor).RestGlow;

    public Color? GlowBaseFor(Control control, bool selected, bool enabled) =>
        GlowBaseFor(color => ResolveColor(control, color), selected, enabled);

    public Color? GlowBaseFor(Func<UiTokens.Color, Color> resolveColor, bool selected, bool enabled) =>
        enabled ? GlowBaseFor(resolveColor, selected) : null;

    public static Color ResolveColor(Control control, UiTokens.Color color) =>
        color == UiTokens.Color.Transparent
            ? Colors.Transparent
            : UiThemeLookup.Color(control, color);
}

/// <summary>Resolved theme colours for one canonical button style.</summary>
public readonly record struct UiResolvedButtonStyle(
    Color Background,
    Color Border,
    Color Content,
    Color Selected,
    Color? RestGlow);

/// <summary>Token-derived geometry shared by all canonical button layouts.</summary>
public readonly record struct UiButtonMetrics(
    float TouchTarget,
    float StandardControlSize,
    float CompactControlSize,
    float BadgeMinimumSize,
    float BadgeOffset,
    float SelectedStroke)
{
    public static UiButtonMetrics Default { get; } = new(
        UiSize.Control.Touch,
        UiSize.Control.Default,
        UiSize.Control.Small,
        UiSize.Widget.BadgeMinimumSize,
        UiSize.Widget.BadgeOffset,
        UiSize.Stroke.ButtonSelected);

    public float ControlSize(UiButtonContentLayout layout) => layout switch
    {
        UiButtonContentLayout.Row => StandardControlSize,
        UiButtonContentLayout.RowCompact => CompactControlSize,
        UiButtonContentLayout.Stacked => TouchTarget,
        _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, null),
    };

    public Vector2 MinimumSize(UiButtonContentLayout layout)
    {
        var size = ControlSize(layout);
        return new Vector2(size, size);
    }

    /// <summary>
    /// The layout picks the icon size, never the call site: <c>icon</c> beside text in a row,
    /// <c>icon-lg</c> in a row with no text and in a stack (reference <c>c_btn</c>/<c>c_ib</c>).
    /// </summary>
    public static UiIconSize IconSize(UiButtonContentLayout layout, bool hasLabel) => layout switch
    {
        UiButtonContentLayout.Row or UiButtonContentLayout.RowCompact => hasLabel ? UiIconSize.Standard : UiIconSize.Large,
        UiButtonContentLayout.Stacked => UiIconSize.Large,
        _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, null),
    };

    public Vector2 BadgePosition(Vector2 controlSize) =>
        new(controlSize.X - BadgeMinimumSize + BadgeOffset, -BadgeOffset);
}

