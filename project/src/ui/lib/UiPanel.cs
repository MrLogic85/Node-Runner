using Godot;
namespace NodeRunner.Ui.Lib;

/// <summary>Reusable token-backed surface for the design-system component kit.</summary>
public partial class UiPanel : PanelContainer
{
    public enum PanelState
    {
        Normal,
        Focused,
        Danger,
    }

    private bool _raised;

    [Export]
    public bool Raised
    {
        get => _raised;
        set
        {
            _raised = value;
            RefreshStyle();
        }
    }

    private PanelState _state;

    [Export]
    public PanelState State
    {
        get => _state;
        set
        {
            _state = value;
            RefreshStyle();
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
        RefreshStyle();
    }

    private void RefreshStyle()
    {
        if (IsInsideTree())
        {
            AddThemeStyleboxOverride("panel", CreateStyle());
        }
    }

    private StyleBoxFlat CreateStyle()
    {
        var border = State == PanelState.Danger ? Tokens.Danger : Tokens.LineStrong;
        var borderWidth = State == PanelState.Focused ? 3 : 1;
        return new StyleBoxFlat
        {
            BgColor = Raised ? Tokens.PanelRaised : Tokens.Panel,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = (int)Tokens.Radius,
            CornerRadiusTopRight = (int)Tokens.Radius,
            CornerRadiusBottomLeft = (int)Tokens.Radius,
            CornerRadiusBottomRight = (int)Tokens.Radius,
        };
    }
}
