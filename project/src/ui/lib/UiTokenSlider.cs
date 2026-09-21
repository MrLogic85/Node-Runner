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
    private TextureRect? _labelLockIcon;
    private HSlider? _slider;
    private Label? _minimumLabel;
    private Label? _valueLabel;
    private Label? _maximumLabel;
    private UiStepperButton? _decrementButton;
    private UiStepperButton? _incrementButton;
    private bool _disabled;
    private bool _showSteppers;
    private bool _locked;
    private bool _compact;
    private double? _defaultMarker;
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

    private double _minValue;
    private double _maxValue = 100;
    private double _value = 50;

    [Export]
    public double MinValue
    {
        get => _minValue;
        set
        {
            _minValue = value;
            NormalizeBounds();
            Refresh();
        }
    }

    [Export]
    public double MaxValue
    {
        get => _maxValue;
        set
        {
            _maxValue = value;
            NormalizeBounds();
            Refresh();
        }
    }

    [Export]
    public double Value
    {
        get => _value;
        set
        {
            _value = UiComponentContracts.ClampValue(value, _minValue, _maxValue);
            RefreshValue();
        }
    }

    [Export]
    public bool ShowSteppers
    {
        get => _showSteppers;
        set
        {
            _showSteppers = value;
            Refresh();
        }
    }

    [Export]
    public bool Locked
    {
        get => _locked;
        set
        {
            _locked = value;
            Refresh();
        }
    }

    [Export]
    public bool Disabled
    {
        get => _disabled;
        set
        {
            _disabled = value;
            Refresh();
        }
    }

    [Export]
    public bool Compact
    {
        get => _compact;
        set
        {
            _compact = value;
            Refresh();
        }
    }

    public double? DefaultMarker
    {
        get => _defaultMarker;
        set
        {
            _defaultMarker = value is null ? null : UiComponentContracts.ClampValue(value.Value, MinValue, MaxValue);
            RefreshValue();
        }
    }

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

        var labelRow = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(Compact ? 70 : 92, 0),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        labelRow.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        AddChild(labelRow);

        _labelLockIcon = UiFieldAndRows.Icon(UiIconId.Lock, UiIconSize.Small, _tokens.Muted);
        labelRow.AddChild(_labelLockIcon);

        _label = new Label
        {
            VerticalAlignment = VerticalAlignment.Center,
        };
        labelRow.AddChild(_label);

        _decrementButton = new UiStepperButton
        {
            Tokens = _tokens,
            Symbol = UiComponentContracts.StepperSymbol.Minus,
        };
        _decrementButton.Pressed += () => SetValue(Value - (_slider?.Step ?? 1));
        AddChild(_decrementButton);

        _slider = new HSlider
        {
            MinValue = MinValue,
            MaxValue = MaxValue,
            Value = Value,
            Step = 1,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, Compact ? 24 : 36),
            MouseFilter = MouseFilterEnum.Pass,
        };
        _slider.ValueChanged += value =>
        {
            Value = value;
            RefreshValue();
            EmitSignal(SignalName.ValueChanged, value);
        };
        AddChild(_slider);

        _incrementButton = new UiStepperButton
        {
            Tokens = _tokens,
            Symbol = UiComponentContracts.StepperSymbol.Plus,
        };
        _incrementButton.Pressed += () => SetValue(Value + (_slider?.Step ?? 1));
        AddChild(_incrementButton);

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

        CustomMinimumSize = new Vector2(0, Compact ? _tokens.ControlSmall : _tokens.TouchTarget);
        AddThemeConstantOverride("separation", (int)_tokens.Space2);
        if (_label is not null)
        {
            _label.Text = LabelText;
            _tokens.ApplyTextStyle(_label, _tokens.LabelText);
            _label.AddThemeColorOverride("font_color", _tokens.Muted);
        }
        if (_labelLockIcon is not null)
        {
            _labelLockIcon.Visible = Locked;
            _labelLockIcon.SelfModulate = _tokens.Muted;
        }

        if (_slider is not null)
        {
            _slider.MinValue = MinValue;
            _slider.MaxValue = MaxValue;
            _slider.Value = Value;
            _slider.Editable = !Locked && !Disabled;
            _slider.CustomMinimumSize = new Vector2(0, Compact ? 24 : 36);
            var inactive = Locked || Disabled;
            _slider.AddThemeStyleboxOverride("slider", _tokens.ControlStyle(_tokens.Line, _tokens.Line, radius: _tokens.RadiusPill));
            _slider.AddThemeStyleboxOverride("grabber_area", _tokens.ControlStyle(inactive ? _tokens.PanelRaised : _tokens.Accent, inactive ? _tokens.LineStrong : _tokens.Accent, radius: _tokens.RadiusPill, glow: !inactive));
            _slider.AddThemeStyleboxOverride("focus", _tokens.FocusRingStyle(_tokens.RadiusPill));
        }

        if (_decrementButton is not null && _incrementButton is not null)
        {
            _decrementButton.Visible = ShowSteppers;
            _incrementButton.Visible = ShowSteppers;
            _decrementButton.Disabled = Locked || Disabled;
            _incrementButton.Disabled = Locked || Disabled;
        }

        RefreshValue();
    }

    private void RefreshValue()
    {
        if (_valueLabel is null || _minimumLabel is null || _maximumLabel is null)
        {
            return;
        }

        _valueLabel.Text = Locked ? "locked" : Value.ToString("0");
        _tokens.ApplyTextStyle(_valueLabel, _tokens.ReadoutText);
        _valueLabel.AddThemeColorOverride("font_color", Disabled ? _tokens.Muted : _tokens.Ink);
        _minimumLabel.Text = DefaultMarker is null ? MinValue.ToString("0") : $"default {DefaultMarker:0}";
        _maximumLabel.Text = MaxValue.ToString("0");
        _tokens.ApplyTextStyle(_minimumLabel, _tokens.ReadoutSmallText);
        _tokens.ApplyTextStyle(_maximumLabel, _tokens.ReadoutSmallText);
        _minimumLabel.AddThemeColorOverride("font_color", _tokens.Muted);
        _maximumLabel.AddThemeColorOverride("font_color", _tokens.Muted);
    }

    private void SetValue(double value)
    {
        if (Locked || Disabled)
        {
            return;
        }

        var clamped = UiComponentContracts.ClampValue(value, MinValue, MaxValue);
        if (Mathf.IsEqualApprox((float)Value, (float)clamped))
        {
            return;
        }

        Value = clamped;
        if (_slider is not null)
        {
            _slider.SetValueNoSignal(clamped);
        }

        EmitSignal(SignalName.ValueChanged, clamped);
    }

    private void NormalizeBounds()
    {
        if (_minValue > _maxValue)
        {
            (_minValue, _maxValue) = (_maxValue, _minValue);
        }

        _value = UiComponentContracts.ClampValue(_value, _minValue, _maxValue);
        if (_defaultMarker is not null)
        {
            _defaultMarker = UiComponentContracts.ClampValue(_defaultMarker.Value, _minValue, _maxValue);
        }
    }
}
