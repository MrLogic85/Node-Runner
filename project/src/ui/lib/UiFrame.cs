using Godot;

namespace NodeRunner.Ui.Lib;

[Tool]
[GlobalClass]
public partial class UiFrame : PanelContainer
{
    private UiTokenType _debugTokenType = UiTokenType.Neon;
    private bool _ready;

    private ColorRect? _background;
    private UiCard? _card;

    [Export]
    public UiTokenType DebugTokenType
    {
        get => _debugTokenType;
        set
        {
            _debugTokenType = value;
            Tokens = UiTokens.FromType(value);
        }
    }

    public UiTokens Tokens
    {
        get;
        set
        {
            field = value;
            ApplyTokens();
        }
    } = UiTokens.Neon;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        _card = GetNode<UiCard>("%Card");
        _background = GetNode<ColorRect>("%Background");
        base._Ready();
        _ready = true;
        ApplyTokens();
    }

    private void ApplyTokens()
    {
        if (!_ready)
            return;

        _card?.Tokens = Tokens;
        _background?.Color = Tokens.Background;
    }
}
