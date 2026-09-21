using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Power value row for draws-up-to and makes/stores semantics.</summary>
public partial class UiPowerRow : HBoxContainer
{
    private UiTokens _tokens = UiTokens.Neon;

    [Export]
    public string TextValue { get; set; } = "Draws up to 0.6";

    [Export]
    public bool Output { get; set; }

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            Rebuild();
        }
    }

    public override void _Ready() => Rebuild();

    private void Rebuild()
    {
        if (!IsInsideTree())
        {
            return;
        }

        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        AddThemeConstantOverride("separation", (int)_tokens.Space2);
        AddChild(UiFieldAndRows.Label("⚡", _tokens, _tokens.LabelText, Output ? _tokens.Output : _tokens.Halo));
        AddChild(UiFieldAndRows.Label(TextValue, _tokens, _tokens.BodyStrongText, _tokens.Ink));
    }
}
