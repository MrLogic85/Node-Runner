using Godot;
namespace NodeRunner.Ui.Lib;

/// <summary>Reusable token-backed surface for the design-system component kit.</summary>
public partial class UiPanel : PanelContainer
{
    /// <summary>Compatibility names retained for existing scene and screen callers.</summary>
    public enum PanelState
    {
        Normal,
        Focused,
        Selected,
        Locked,
        Warning,
        Danger,
        Hint,
    }

    private UiSurfaceContracts.FrameVariant _variant = UiSurfaceContracts.FrameVariant.Frame;
    private UiSurfaceContracts.FrameSize _size = UiSurfaceContracts.FrameSize.Default;

    [Export]
    public bool Raised
    {
        get => _variant == UiSurfaceContracts.FrameVariant.Raised;
        set
        {
            if (value)
            {
                Variant = UiSurfaceContracts.FrameVariant.Raised;
            }
            else if (_variant == UiSurfaceContracts.FrameVariant.Raised)
            {
                Variant = UiSurfaceContracts.FrameVariant.Frame;
            }
        }
    }

    [Export]
    public UiSurfaceContracts.FrameVariant Variant
    {
        get => _variant;
        set
        {
            _variant = value;
            RefreshStyle();
        }
    }

    [Export]
    public new UiSurfaceContracts.FrameSize Size
    {
        get => _size;
        set
        {
            _size = value;
            RefreshStyle();
        }
    }

    [Export]
    public bool Tight
    {
        get => _size is UiSurfaceContracts.FrameSize.Snug or UiSurfaceContracts.FrameSize.Tight;
        set
        {
            Size = value
                ? UiSurfaceContracts.FrameSize.Snug
                : UiSurfaceContracts.FrameSize.Default;
        }
    }

    [Export]
    public bool Flush
    {
        get => _size == UiSurfaceContracts.FrameSize.Flush;
        set
        {
            Size = value
                ? UiSurfaceContracts.FrameSize.Flush
                : UiSurfaceContracts.FrameSize.Default;
        }
    }

    [Export]
    public PanelState State
    {
        get => Variant switch
        {
            UiSurfaceContracts.FrameVariant.Sel => PanelState.Selected,
            UiSurfaceContracts.FrameVariant.Pick => PanelState.Focused,
            UiSurfaceContracts.FrameVariant.Lock => PanelState.Locked,
            UiSurfaceContracts.FrameVariant.Warn => PanelState.Warning,
            UiSurfaceContracts.FrameVariant.Hint => PanelState.Hint,
            UiSurfaceContracts.FrameVariant.Raised => PanelState.Normal,
            _ => PanelState.Normal,
        };
        set
        {
            Variant = value switch
            {
                PanelState.Focused => UiSurfaceContracts.FrameVariant.Pick,
                PanelState.Selected => UiSurfaceContracts.FrameVariant.Sel,
                PanelState.Locked => UiSurfaceContracts.FrameVariant.Lock,
                PanelState.Warning or PanelState.Danger => UiSurfaceContracts.FrameVariant.Warn,
                PanelState.Hint => UiSurfaceContracts.FrameVariant.Hint,
                _ => UiSurfaceContracts.FrameVariant.Frame,
            };
        }
    }

    private UiTokens _tokens = UiTokens.Neon;

    public UiTokens Tokens
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
        FocusMode = FocusModeEnum.All;
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

    private StyleBoxFlat CreateStyle()
    {
        var style = Tokens.FrameStyle(Variant, Size);
        if (UiSurfaceContracts.IsDimmed(Variant))
        {
            style.BorderWidthLeft = 0;
            style.BorderWidthTop = 0;
            style.BorderWidthRight = 0;
            style.BorderWidthBottom = 0;
            SelfModulate = new Color(1, 1, 1, 0.55f);
        }
        else
        {
            SelfModulate = Colors.White;
        }

        return style;
    }

    public override void _Draw()
    {
        base._Draw();
        if (!UiSurfaceContracts.IsDashed(Variant))
        {
            return;
        }

        DrawDashedBorder(Tokens.LineStrong);
    }

    private void DrawDashedBorder(Color color)
    {
        const float dash = 4;
        const float gap = 3;
        var inset = Tokens.RadiusLarge * 0.5f;
        DrawDashedLine(new Vector2(inset, 0), new Vector2(base.Size.X - inset, 0), color, dash, gap);
        DrawDashedLine(new Vector2(inset, base.Size.Y), new Vector2(base.Size.X - inset, base.Size.Y), color, dash, gap);
        DrawDashedLine(new Vector2(0, inset), new Vector2(0, base.Size.Y - inset), color, dash, gap);
        DrawDashedLine(new Vector2(base.Size.X, inset), new Vector2(base.Size.X, base.Size.Y - inset), color, dash, gap);
    }

    private void DrawDashedLine(Vector2 from, Vector2 to, Color color, float dashLength, float gapLength)
    {
        var direction = to - from;
        var length = direction.Length();
        if (length <= 0)
        {
            return;
        }

        direction /= length;
        for (var distance = 0f; distance < length; distance += dashLength + gapLength)
        {
            DrawLine(
                from + (direction * distance),
                from + (direction * Math.Min(distance + dashLength, length)),
                color,
                Tokens.StrokeHair,
                antialiased: true);
        }
    }
}
