using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Label/value row paired with the canonical progress bar.</summary>
public partial class UiMeterRow : VBoxContainer
{
    private UiSlider? _bar;

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
                _bar.Value = UiSliderValue.Progress(_percent / 100d);
            }
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        Rebuild();
    }

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

        AddThemeConstantOverride("separation", UiSize.Space.S1);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", UiSize.Space.S2);
        AddChild(row);
        var label = UiFieldAndRows.Label(LabelText,
            UiTokens.Typography.Caption,
            UiTokens.Color.Muted);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(label);
        row.AddChild(UiFieldAndRows.Label(ValueText,
            UiTokens.Typography.ReadoutMedium,
            UiTokens.Color.Ink,
            HorizontalAlignment.Right));
        _bar = new UiSlider
        {
            LabelText = string.Empty,
            ReadoutText = string.Empty,
            Value = UiSliderValue.Progress(Percent / 100d),
        };
        AddChild(_bar);
    }
}
