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
    Row,
    Icon,
    Stack,
}

/// <summary>
/// Composes a button's semantic colours independently from its layout and state.
/// A rest glow is optional; selected glow is always derived from
/// <see cref="SelectedColor"/>.
/// </summary>
public readonly record struct UiButtonStyle(
    UiButtonKind Kind,
    UiColor BackgroundColor,
    UiColor BorderColor,
    UiColor ContentColor,
    UiColor SelectedColor,
    UiColor? RestGlowColor = null)
{
    public static UiButtonStyle Primary { get; } = new(
        UiButtonKind.Primary,
        UiColor.Accent,
        UiColor.Accent,
        UiColor.OnAccent,
        UiColor.Halo,
        UiColor.Accent);

    public static UiButtonStyle Secondary { get; } = new(
        UiButtonKind.Secondary,
        UiColor.PanelRaised,
        UiColor.LineStrong,
        UiColor.Ink,
        UiColor.Accent);

    public static UiButtonStyle Tertiary { get; } = new(
        UiButtonKind.Tertiary,
        UiColor.PanelRaised,
        UiColor.Danger,
        UiColor.Danger,
        UiColor.Danger);

    public static UiButtonStyle Flat { get; } = new(
        UiButtonKind.Flat,
        UiColor.Transparent,
        UiColor.Transparent,
        UiColor.Ink,
        UiColor.Ink);

    public static UiButtonStyle For(UiButtonKind kind) => kind switch
    {
        UiButtonKind.Primary => Primary,
        UiButtonKind.Secondary => Secondary,
        UiButtonKind.Tertiary => Tertiary,
        UiButtonKind.Flat => Flat,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public UiResolvedButtonStyle Resolve(UiTokens tokens) => new(
        ResolveColor(BackgroundColor, tokens),
        ResolveColor(BorderColor, tokens),
        ResolveColor(ContentColor, tokens),
        ResolveColor(SelectedColor, tokens),
        RestGlowColor is { } restGlow ? ResolveColor(restGlow, tokens) : null);

    public Color BorderFor(UiTokens tokens, bool selected) =>
        selected ? Resolve(tokens).Selected : Resolve(tokens).Border;

    public Color? GlowBaseFor(UiTokens tokens, bool selected) =>
        selected ? Resolve(tokens).Selected : Resolve(tokens).RestGlow;

    public Color? GlowBaseFor(UiTokens tokens, bool selected, bool enabled) =>
        enabled ? GlowBaseFor(tokens, selected) : null;

    public static Color ResolveColor(UiColor color, UiTokens tokens) => color switch
    {
        UiColor.Transparent => Colors.Transparent,
        UiColor.PanelRaised => tokens.PanelRaised,
        UiColor.LineStrong => tokens.LineStrong,
        UiColor.Ink => tokens.Ink,
        UiColor.Muted => tokens.Muted,
        UiColor.Accent => tokens.Accent,
        UiColor.OnAccent => tokens.OnAccent,
        UiColor.Halo => tokens.Halo,
        UiColor.Danger => tokens.Danger,
        _ => throw new ArgumentOutOfRangeException(nameof(color), color, null),
    };
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
    public static UiButtonMetrics From(UiTokens tokens) => new(
        tokens.TouchTarget,
        tokens.ControlHeight,
        tokens.ControlSmall,
        tokens.BadgeMinimumSize,
        tokens.BadgeOffset,
        tokens.ButtonSelectedStroke);

    public float VisibleControlSize(UiButtonContentLayout layout, bool compact) => layout switch
    {
        UiButtonContentLayout.Row => compact ? CompactControlSize : StandardControlSize,
        UiButtonContentLayout.Icon => compact ? CompactControlSize : StandardControlSize,
        UiButtonContentLayout.Stack => TouchTarget,
        _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, null),
    };

    public Vector2 MinimumSize(UiButtonContentLayout layout, bool compact)
    {
        var target = compact && layout != UiButtonContentLayout.Stack
            ? CompactControlSize
            : TouchTarget;
        return layout switch
        {
            UiButtonContentLayout.Row => new Vector2(0, target),
            UiButtonContentLayout.Icon or UiButtonContentLayout.Stack => new Vector2(target, target),
            _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, null),
        };
    }

    public Rect2 VisibleFrame(
        Vector2 controlSize,
        UiButtonContentLayout layout,
        bool compact)
    {
        var visibleSize = VisibleControlSize(layout, compact);
        var width = layout == UiButtonContentLayout.Row ? controlSize.X : visibleSize;
        return new Rect2(
            (controlSize.X - width) * 0.5f,
            (controlSize.Y - visibleSize) * 0.5f,
            width,
            visibleSize);
    }

    /// <summary>
    /// Local geometry of the hold-progress layers inside the visible frame.
    /// <c>Fill</c> is progress independent so the fill keeps the
    /// frame's rounded contour; <c>Reveal</c> is the rectangular
    /// window that exposes the held fraction of it.
    /// </summary>
    public static UiButtonProgressLayout ProgressLayout(Rect2 visibleFrame, float progress)
    {
        var width = Mathf.Max(0, visibleFrame.Size.X);
        var height = Mathf.Max(0, visibleFrame.Size.Y);
        return new UiButtonProgressLayout(
            new Rect2(Vector2.Zero, new Vector2(width, height)),
            new Rect2(
                Vector2.Zero,
                new Vector2(
                    UiComponentContracts.ButtonProgressRevealWidth(width, progress),
                    height)));
    }

    public Vector2 BadgePosition(
        Vector2 controlSize,
        UiButtonContentLayout layout,
        bool compact)
    {
        var frame = VisibleFrame(controlSize, layout, compact);
        return new Vector2(
            frame.End.X - BadgeMinimumSize + BadgeOffset,
            frame.Position.Y - BadgeOffset);
    }
}

/// <summary>Local rects of the hold-progress fill and its reveal window.</summary>
public readonly record struct UiButtonProgressLayout(Rect2 Fill, Rect2 Reveal);
