using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Label/help composition around a native CheckButton or CheckBox.</summary>
public abstract partial class UiChoiceRow : Container
{
    [Signal]
    public delegate void ToggledEventHandler(bool on);

    private UiTokens _tokens = UiTokens.Neon;
    private string _labelText = string.Empty;
    private string _subtext = string.Empty;
    private bool _selected;
    private bool _disabled;
    private Button? _button;
    private VBoxContainer? _labels;
    private Label? _label;
    private Label? _help;

    protected abstract bool IsSwitch { get; }

    protected bool Selected
    {
        get => _button?.ButtonPressed ?? _selected;
        set
        {
            _selected = value;
            _button?.SetPressedNoSignal(value);
        }
    }

    [Export]
    public bool Disabled
    {
        get => _disabled;
        set
        {
            _disabled = value;
            if (_button is not null)
            {
                _button.Disabled = value;
            }
            RefreshLabels();
        }
    }

    [Export]
    public string LabelText
    {
        get => _labelText;
        set
        {
            _labelText = value;
            RefreshLabels();
        }
    }

    [Export]
    public string Subtext
    {
        get => _subtext;
        set
        {
            _subtext = value;
            RefreshLabels();
        }
    }

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            ApplyTheme();
            RefreshLabels();
        }
    }

    public override void _Ready()
    {
        if (_button is null)
        {
            MouseFilter = MouseFilterEnum.Pass;
            _button = IsSwitch ? new CheckButton() : new CheckBox();
            _button.Disabled = Disabled;
            _button.SetPressedNoSignal(_selected);
            _button.Toggled += OnNativeToggled;
            AddChild(_button);
            _labels = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            _labels.AddThemeConstantOverride("separation", 0);
            _label = new Label { MouseFilter = MouseFilterEnum.Ignore };
            _help = new Label { MouseFilter = MouseFilterEnum.Ignore };
            _labels.AddChild(_label);
            _labels.AddChild(_help);
            _button.AddChild(_labels);
            _labels.MinimumSizeChanged += RefreshMinimumSize;
        }

        ApplyTheme();
        RefreshLabels();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationSortChildren && _button is not null && _labels is not null)
        {
            FitChildInRect(_button, new Rect2(Vector2.Zero, Size));
            var indicator = IndicatorSize();
            var inset = indicator.X + Tokens.Space2;
            var height = _labels.GetCombinedMinimumSize().Y;
            _labels.Position = new Vector2(IsSwitch ? 0 : inset, (Size.Y - height) * 0.5f);
            _labels.Size = new Vector2(Mathf.Max(0, Size.X - inset), height);
        }
    }

    private void OnNativeToggled(bool on)
    {
        _selected = on;
        EmitSignal(SignalName.Toggled, on);
    }

    private void RefreshMinimumSize()
    {
        var content = _labels?.GetCombinedMinimumSize() ?? Vector2.Zero;
        CustomMinimumSize = new Vector2(
            content.X + IndicatorSize().X + Tokens.Space2,
            Mathf.Max(Tokens.TouchTarget, content.Y));
        QueueSort();
    }

    private void RefreshLabels()
    {
        if (_button is null || _labels is null)
        {
            return;
        }

        _label!.Text = LabelText;
        _help!.Text = Subtext;
        _help.Visible = !string.IsNullOrWhiteSpace(Subtext);
        Tokens.ApplyTextStyle(_label, Tokens.SmallStrongText);
        Tokens.ApplyTextStyle(_help, Tokens.NoteText);
        _label.AddThemeColorOverride("font_color", Tokens.Ink);
        _help.AddThemeColorOverride("font_color", Tokens.Muted);
        _labels.Modulate = new Color(1, 1, 1, Disabled ? UiChoiceStyle.DisabledOpacity : 1);
        _button.TooltipText = string.IsNullOrWhiteSpace(Subtext) ? LabelText : $"{LabelText}\n{Subtext}";
        _button.AccessibilityName = LabelText;
        _button.AccessibilityDescription = Subtext;
        RefreshMinimumSize();
    }

    private void ApplyTheme()
    {
        if (_button is null)
        {
            return;
        }

        _button.Theme = UiChoiceTheme.Create(Tokens, IsSwitch);
    }

    private Vector2 IndicatorSize()
    {
        if (_button is null)
        {
            return UiChoiceStyle.IndicatorSize(Tokens, IsSwitch);
        }

        return _button.GetThemeIcon("checked").GetSize()
            .Max(_button.GetThemeIcon("unchecked").GetSize());
    }
}
