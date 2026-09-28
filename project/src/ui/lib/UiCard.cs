using Godot;
namespace NodeRunner.Ui.Lib;

/// <summary>Canonical token-backed card/frame surface for the design-system component kit.</summary>
[Tool]
[GlobalClass]
public partial class UiCard : PanelContainer, IUiClipping
{
    public enum CardVariant
    {
        Frame,
        Selected,
        Locked,
        Warning,
        Hint,
        Raised,
    }

    public enum CardSize
    {
        Default,
        Snug,
        Tight,
        Roomy,
        Flush,
    }

    private CardVariant _kind = CardVariant.Frame;
    private CardSize _size = CardSize.Default;
    private bool _glow;
    private bool _disabled;
    private bool _refreshingStyle;
    private bool _clipContent;
    private Control? _borderOverlay;
    private StyleBoxFlat? _borderOnlyStyle;

    [Export]
    public CardVariant Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            RefreshStyle();
        }
    }

    [Export]
    public CardSize SizeVariant
    {
        get => _size;
        set
        {
            _size = value;
            RefreshStyle();
        }
    }

    [Export]
    public bool Glow
    {
        get => _glow;
        set
        {
            _glow = value;
            RefreshStyle();
        }
    }

    [Export]
    public bool Disabled
    {
        get => _disabled;
        set
        {
            _disabled = value;
            RefreshStyle();
        }
    }

    /// <summary>
    /// Clips the content to the card's rounded shape (content that reaches the edge, a
    /// square background or a glow, stays inside the corners) and draws the border over it.
    /// Inside another clipping node the content is left unclipped; see <see cref="UiClip"/>.
    /// </summary>
    [Export]
    public bool ClipContent
    {
        get => _clipContent;
        set
        {
            _clipContent = value;
            RefreshClip();
        }
    }

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.None;
        MouseFilter = MouseFilterEnum.Pass;
        RefreshStyle();
    }

    private readonly UiUnsavedState _unsaved = new("theme_override_styles/panel", CanvasItem.PropertyName.ClipChildren);

    public override void _Notification(int what)
    {
        if (_unsaved.Handle(this, what, RefreshStyle))
        {
            return;
        }

        if (what == NotificationThemeChanged)
        {
            RefreshStyle();
        }
        else if (what == NotificationEnterTree)
        {
            RefreshClip();
        }
    }

    protected void RefreshStyle()
    {
        if (!IsInsideTree() || _refreshingStyle)
        {
            return;
        }

        _refreshingStyle = true;
        try
        {
            var style = CreateStyle();
            AddThemeStyleboxOverride("panel", style);
            _borderOnlyStyle = BorderOnly(style);
            QueueRedraw();
            _borderOverlay?.QueueRedraw();
        }
        finally
        {
            _refreshingStyle = false;
        }
    }

    protected virtual StyleBoxFlat CreateStyle()
    {
        Modulate = Disabled
            ? Colors.White.ScaleAlpha(0.5f)
            : Colors.White;

        var style = UiThemeLookup.CreateFrameStyleBox(
            this,
            ToFrameVariant(Kind),
            ToFrameSize(SizeVariant),
            glow: Glow && !Disabled);
        if (Kind == CardVariant.Raised)
        {
            SetContentMargin(style, PaddingFor(SizeVariant));
            SetCornerRadius(style, UiSize.Radius.Large);
        }

        if (Kind == CardVariant.Locked || Disabled)
        {
            style.BgColor = UiThemeLookup.Color(this, UiTokens.Color.Panel);
            style.BorderWidthLeft = 0;
            style.BorderWidthTop = 0;
            style.BorderWidthRight = 0;
            style.BorderWidthBottom = 0;
        }

        return style;
    }

    public override void _Draw()
    {
        base._Draw();
        if (!HasDashedBorder)
        {
            return;
        }

        DrawDashedBorder(this, Vector2.Zero);
    }

    private bool HasDashedBorder => Disabled || Kind == CardVariant.Locked;

    // Also run on entering the tree: whether an ancestor already clips decides if this card may.
    void IUiClipping.RefreshClip() => RefreshClip();

    private void RefreshClip()
    {
        UiClip.Apply(this, _clipContent);
        if (_clipContent && _borderOverlay is null)
        {
            // Internal and last, so the content is clipped to the card's shape but the
            // border still draws on top of whatever reaches the edge.
            _borderOverlay = new Control { MouseFilter = MouseFilterEnum.Ignore };
            _borderOverlay.Draw += DrawBorderOverContent;
            AddChild(_borderOverlay, @internal: InternalMode.Back);
        }
        else if (!_clipContent && _borderOverlay is not null)
        {
            _borderOverlay.QueueFree();
            _borderOverlay = null;
        }
    }

    // The overlay is laid out inside the content margins; draw back out to the card's rect.
    private void DrawBorderOverContent()
    {
        if (_borderOverlay is null)
        {
            return;
        }

        var origin = -_borderOverlay.Position;
        if (_borderOnlyStyle is not null)
        {
            _borderOverlay.DrawStyleBox(_borderOnlyStyle, new Rect2(origin, Size));
        }

        if (HasDashedBorder)
        {
            DrawDashedBorder(_borderOverlay, origin);
        }
    }

    private static StyleBoxFlat BorderOnly(StyleBoxFlat style)
    {
        var border = (StyleBoxFlat)style.Duplicate();
        border.DrawCenter = false;
        border.ShadowSize = 0;
        return border;
    }

    private Color DashedBorderColor() =>
        Kind switch
        {
            CardVariant.Selected or CardVariant.Locked => UiThemeLookup.Color(this, UiTokens.Color.Accent),
            CardVariant.Warning => UiThemeLookup.Color(this, UiTokens.Color.Danger),
            CardVariant.Hint => UiThemeLookup.Color(this, UiTokens.Color.Halo),
            CardVariant.Raised => UiThemeLookup.Color(this, UiTokens.Color.LineStrong),
            _ => UiThemeLookup.Color(this, UiTokens.Color.Edge),
        };

    private void DrawDashedBorder(CanvasItem canvas, Vector2 origin)
    {
        var stroke = UiSize.Stroke.Hair;
        var rect = new Rect2(
            origin + new Vector2(stroke * 0.5f, stroke * 0.5f),
            new Vector2(Math.Max(0, base.Size.X - stroke), Math.Max(0, base.Size.Y - stroke)));
        UiDashedBorder.DrawRoundedRect(canvas, rect, Math.Max(0, UiSize.Radius.Large - (stroke * 0.5f)), DashedBorderColor(), stroke);
    }

    private static UiSurfaceContracts.FrameVariant ToFrameVariant(CardVariant variant) =>
        variant switch
        {
            CardVariant.Selected => UiSurfaceContracts.FrameVariant.Sel,
            CardVariant.Locked => UiSurfaceContracts.FrameVariant.Lock,
            CardVariant.Warning => UiSurfaceContracts.FrameVariant.Warn,
            CardVariant.Hint => UiSurfaceContracts.FrameVariant.Hint,
            CardVariant.Raised => UiSurfaceContracts.FrameVariant.Raised,
            _ => UiSurfaceContracts.FrameVariant.Frame,
        };

    private static UiSurfaceContracts.FrameSize ToFrameSize(CardSize size) =>
        size switch
        {
            CardSize.Snug => UiSurfaceContracts.FrameSize.Snug,
            CardSize.Tight => UiSurfaceContracts.FrameSize.Tight,
            CardSize.Roomy => UiSurfaceContracts.FrameSize.Roomy,
            CardSize.Flush => UiSurfaceContracts.FrameSize.Flush,
            _ => UiSurfaceContracts.FrameSize.Default,
        };

    private float PaddingFor(CardSize size) =>
        size switch
        {
            CardSize.Snug => UiSize.Space.S2,
            CardSize.Tight => UiSize.Space.S1,
            CardSize.Roomy => UiSize.Space.S4,
            CardSize.Flush => 0,
            _ => UiSize.Space.S3,
        } + UiSize.Stroke.Hair;

    private static void SetContentMargin(StyleBoxFlat style, float value)
    {
        style.ContentMarginLeft = value;
        style.ContentMarginTop = value;
        style.ContentMarginRight = value;
        style.ContentMarginBottom = value;
    }

    private static void SetCornerRadius(StyleBoxFlat style, float value)
    {
        var radius = (int)value;
        style.CornerRadiusTopLeft = radius;
        style.CornerRadiusTopRight = radius;
        style.CornerRadiusBottomLeft = radius;
        style.CornerRadiusBottomRight = radius;
    }
}
