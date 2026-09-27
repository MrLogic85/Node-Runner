using Godot;
namespace NodeRunner.Ui.Lib;

/// <summary>Canonical token-backed card/frame surface for the design-system component kit.</summary>
[Tool]
[GlobalClass]
public partial class UiCard : PanelContainer
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

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.None;
        MouseFilter = MouseFilterEnum.Pass;
        RefreshStyle();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged)
        {
            RefreshStyle();
        }
    }

    private void RefreshStyle()
    {
        if (!IsInsideTree() || _refreshingStyle)
        {
            return;
        }

        _refreshingStyle = true;
        try
        {
            AddThemeStyleboxOverride("panel", CreateStyle());
            QueueRedraw();
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
        if (!Disabled && Kind != CardVariant.Locked)
        {
            return;
        }

        DrawDashedBorder(DashedBorderColor());
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

    private void DrawDashedBorder(Color color)
    {
        var stroke = UiSize.Stroke.Hair;
        var rect = new Rect2(
            new Vector2(stroke * 0.5f, stroke * 0.5f),
            new Vector2(Math.Max(0, base.Size.X - stroke), Math.Max(0, base.Size.Y - stroke)));
        UiDashedBorder.DrawRoundedRect(this, rect, Math.Max(0, UiSize.Radius.Large - (stroke * 0.5f)), color, stroke);
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
