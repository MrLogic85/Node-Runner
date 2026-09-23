using Godot;
namespace NodeRunner.Ui.Lib;

/// <summary>Canonical token-backed card/frame surface for the design-system component kit.</summary>
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

    private UiTokens _tokens = UiTokens.Neon;

    public virtual UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            RefreshStyle();
        }
    }

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.None;
        MouseFilter = MouseFilterEnum.Pass;
        RefreshStyle();
    }

    private void RefreshStyle()
    {
        if (IsInsideTree())
        {
            AddThemeStyleboxOverride("panel", CreateStyle());
            QueueRedraw();
        }
    }

    protected virtual StyleBoxFlat CreateStyle()
    {
        var style = Tokens.FrameStyle(ToFrameVariant(Kind), ToFrameSize(SizeVariant), glow: Glow);
        if (Kind == CardVariant.Raised)
        {
            SetContentMargin(style, PaddingFor(SizeVariant));
            SetCornerRadius(style, Tokens.RadiusLarge);
        }

        if (Kind == CardVariant.Locked)
        {
            style.BgColor = Tokens.Panel;
            style.BorderWidthLeft = 0;
            style.BorderWidthTop = 0;
            style.BorderWidthRight = 0;
            style.BorderWidthBottom = 0;
            Modulate = new Color(1, 1, 1, 0.7f);
        }
        else
        {
            Modulate = Colors.White;
        }

        return style;
    }

    public override void _Draw()
    {
        base._Draw();
        if (Kind != CardVariant.Locked)
        {
            return;
        }

        DrawDashedBorder(Tokens.LineStrong);
    }

    private void DrawDashedBorder(Color color)
    {
        var stroke = Tokens.StrokeHair;
        var rect = new Rect2(
            new Vector2(stroke * 0.5f, stroke * 0.5f),
            new Vector2(Math.Max(0, base.Size.X - stroke), Math.Max(0, base.Size.Y - stroke)));
        UiDashedBorder.DrawRoundedRect(this, rect, Math.Max(0, Tokens.RadiusLarge - (stroke * 0.5f)), color, stroke);
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
            CardSize.Snug => Tokens.Space2,
            CardSize.Tight => Tokens.Space1,
            CardSize.Roomy => Tokens.Space4,
            CardSize.Flush => 0,
            _ => Tokens.Space3,
        } + Tokens.StrokeHair;

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
