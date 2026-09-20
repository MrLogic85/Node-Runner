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
        var border = State switch
        {
            PanelState.Danger => Tokens.Danger,
            PanelState.Focused => Tokens.LineStrong,
            _ => Tokens.Edge,
        };
        var borderWidth = State == PanelState.Focused ? Tokens.StrokeBeam : Tokens.StrokeHair;
        return Tokens.PanelStyle(Raised, border, borderWidth);
    }
}
