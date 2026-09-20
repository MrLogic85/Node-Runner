using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Touch-safe numeric slider row with a live value label.</summary>
public partial class UiTokenSlider : HBoxContainer
{
    [Signal]
    public delegate void ValueChangedEventHandler(double value);

    [Signal]
    public delegate void ExactValueRequestedEventHandler(double currentValue);

    private UiTokens _tokens = UiTokens.Neon;
    private Label? _label;
    private HSlider? _slider;
    private Label? _minimumLabel;
    private Label? _valueLabel;
    private Label? _maximumLabel;
    private string _labelText = "Value";

    [Export]
    public string LabelText
    {
        get => _labelText;
        set
        {
            _labelText = value;
            Refresh();
        }
    }

    [Export]
    public double MinValue { get; set; }

    [Export]
    public double MaxValue { get; set; } = 100;

    [Export]
    public double Value { get; set; } = 50;

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            Refresh();
        }
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, _tokens.TouchTarget);
        AddThemeConstantOverride("separation", (int)_tokens.Space2);

        _label = new Label
        {
            CustomMinimumSize = new Vector2(92, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        AddChild(_label);

        _slider = new HSlider
        {
            MinValue = MinValue,
            MaxValue = MaxValue,
            Value = Value,
            Step = 1,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 36),
        };
        _slider.ValueChanged += value =>
        {
            Value = value;
            RefreshValue();
            EmitSignal(SignalName.ValueChanged, value);
        };
        AddChild(_slider);

        var valueColumn = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(84, 0),
        };
        valueColumn.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        AddChild(valueColumn);

        _valueLabel = new Label
        {
            CustomMinimumSize = new Vector2(84, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Stop,
        };
        _valueLabel.GuiInput += inputEvent =>
        {
            if (inputEvent is InputEventScreenTouch { Pressed: true } ||
                inputEvent is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                EmitSignal(SignalName.ExactValueRequested, Value);
            }
        };
        valueColumn.AddChild(_valueLabel);

        var range = new HBoxContainer();
        range.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        valueColumn.AddChild(range);
        _minimumLabel = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _maximumLabel = new Label { HorizontalAlignment = HorizontalAlignment.Right };
        range.AddChild(_minimumLabel);
        range.AddChild(_maximumLabel);

        Refresh();
    }

    private void Refresh()
    {
        if (!IsInsideTree())
        {
            return;
        }

        CustomMinimumSize = new Vector2(0, _tokens.TouchTarget);
        AddThemeConstantOverride("separation", (int)_tokens.Space2);
        if (_label is not null)
        {
            _label.Text = LabelText;
            _tokens.ApplyTextStyle(_label, _tokens.LabelText);
            _label.AddThemeColorOverride("font_color", _tokens.Muted);
        }

        if (_slider is not null)
        {
            _slider.MinValue = MinValue;
            _slider.MaxValue = MaxValue;
            _slider.Value = Value;
            _slider.CustomMinimumSize = new Vector2(0, 36);
            _slider.AddThemeStyleboxOverride("slider", _tokens.ControlStyle(_tokens.Line, _tokens.Line, radius: _tokens.RadiusPill));
            _slider.AddThemeStyleboxOverride("grabber_area", _tokens.ControlStyle(_tokens.Accent, _tokens.Accent, radius: _tokens.RadiusPill, glow: true));
        }

        RefreshValue();
    }

    private void RefreshValue()
    {
        if (_valueLabel is null || _minimumLabel is null || _maximumLabel is null)
        {
            return;
        }

        _valueLabel.Text = Value.ToString("0");
        _tokens.ApplyTextStyle(_valueLabel, _tokens.ReadoutLargeText);
        _valueLabel.AddThemeColorOverride("font_color", _tokens.Ink);
        _minimumLabel.Text = MinValue.ToString("0");
        _maximumLabel.Text = MaxValue.ToString("0");
        _tokens.ApplyTextStyle(_minimumLabel, _tokens.ReadoutSmallText);
        _tokens.ApplyTextStyle(_maximumLabel, _tokens.ReadoutSmallText);
        _minimumLabel.AddThemeColorOverride("font_color", _tokens.Muted);
        _maximumLabel.AddThemeColorOverride("font_color", _tokens.Muted);
    }
}
