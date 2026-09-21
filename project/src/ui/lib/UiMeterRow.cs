using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Label/value row paired with the canonical progress bar.</summary>
public partial class UiMeterRow : VBoxContainer
{
    private UiTokens _tokens = UiTokens.Neon;
    private UiProgressBar? _bar;

    [Export]
    public string LabelText { get; set; } = "Battery";

    [Export]
    public string ValueText { get; set; } = "62%";

    private float _percent = 62;

    [Export(PropertyHint.Range, "0,100,1")]
    public float Percent
    {
        get => _percent;
        set
        {
            _percent = (float)UiComponentContracts.ClampPercent(value);
            if (_bar is not null)
            {
                _bar.Percent = _percent;
            }
        }
    }

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

        AddThemeConstantOverride("separation", (int)_tokens.Space1);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        AddChild(row);
        var label = UiFieldAndRows.Label(LabelText, _tokens, _tokens.CaptionText, _tokens.Muted);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(label);
        row.AddChild(UiFieldAndRows.Label(ValueText, _tokens, _tokens.ReadoutMediumText, _tokens.Ink, HorizontalAlignment.Right));
        _bar = new UiProgressBar { Tokens = _tokens, Percent = Percent, ShowPercent = false };
        AddChild(_bar);
    }
}
