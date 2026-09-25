using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Label/help composition around a native CheckButton or CheckBox.</summary>
public abstract partial class UiChoiceRow : Container, ISerializationListener
{
    [Signal]
    public delegate void ToggledEventHandler(bool on);

    private sealed record Content(Button Button, VBoxContainer Labels, Label Label, Label Help);

    private UiTokens _tokens = UiTokens.Neon;
    private string _labelText = string.Empty;
    private string _subtext = string.Empty;
    private bool _selected;
    private bool _disabled;
    private Content? _content;
    // Object/method callables survive assembly reloads without retaining managed delegates.
    private Callable ToggledCallback => new(this, MethodName.OnNativeToggled);
    private Callable MinimumSizeChangedCallback => new(this, MethodName.UpdateLayout);

    protected abstract bool IsSwitch { get; }

    protected bool Selected
    {
        get => _content?.Button.ButtonPressed ?? _selected;
        set
        {
            _selected = value;
            _content?.Button.SetPressedNoSignal(value);
        }
    }

    [Export]
    public bool Disabled
    {
        get => _disabled;
        set
        {
            _disabled = value;
            ApplyState();
        }
    }

    [Export]
    public string LabelText
    {
        get => _labelText;
        set
        {
            _labelText = value;
            ApplyContent();
        }
    }

    [Export]
    public string Subtext
    {
        get => _subtext;
        set
        {
            _subtext = value;
            ApplyContent();
        }
    }

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            ApplyTheme();
            ApplyState();
        }
    }

    public override void _EnterTree() => RequestReady();

    public override void _ExitTree() => DisconnectContent();

    public void OnBeforeSerialize() => DisconnectContent();

    public void OnAfterDeserialize() => CallDeferred(MethodName.RestoreContent);

    private void RestoreContent()
    {
        if (IsInsideTree())
        {
            InitializeContent();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        InitializeContent();
    }

    private void InitializeContent()
    {
        DisconnectContent();
        _content = RecoverContent() ?? CreateContent();
        ConnectContent();
        ApplyContent();
        ApplyTheme();
        ApplyState();
    }

    private Content? RecoverContent()
    {
        foreach (Node child in GetChildren(includeInternal: true))
        {
            if (child is not Button button
                || IsSwitch && button is not CheckButton
                || !IsSwitch && button is not CheckBox)
            {
                continue;
            }

            foreach (Node buttonChild in button.GetChildren(includeInternal: true))
            {
                if (buttonChild is not VBoxContainer labels)
                {
                    continue;
                }

                var labelChildren = labels.GetChildren(includeInternal: true)
                    .OfType<Label>()
                    .ToArray();
                if (labelChildren.Length >= 2)
                {
                    return new Content(button, labels, labelChildren[0], labelChildren[1]);
                }
            }
        }

        return null;
    }

    private Content CreateContent()
    {
        Button button = IsSwitch ? new CheckButton() : new CheckBox();
        button.MouseFilter = MouseFilterEnum.Pass;
        AddChild(button, false, InternalMode.Front);

        var labels = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        labels.AddThemeConstantOverride("separation", 0);
        button.AddChild(labels, false, InternalMode.Front);

        var label = new Label { MouseFilter = MouseFilterEnum.Ignore };
        var help = new Label { MouseFilter = MouseFilterEnum.Ignore };
        labels.AddChild(label, false, InternalMode.Front);
        labels.AddChild(help, false, InternalMode.Front);
        return new Content(button, labels, label, help);
    }

    private void ConnectContent()
    {
        if (_content is not { } content)
        {
            return;
        }

        if (!content.Button.IsConnected(BaseButton.SignalName.Toggled, ToggledCallback))
        {
            content.Button.Connect(BaseButton.SignalName.Toggled, ToggledCallback);
        }
        if (!content.Labels.IsConnected(Control.SignalName.MinimumSizeChanged, MinimumSizeChangedCallback))
        {
            content.Labels.Connect(Control.SignalName.MinimumSizeChanged, MinimumSizeChangedCallback);
        }
    }

    private void DisconnectContent()
    {
        if (_content is not { } content)
        {
            return;
        }

        if (content.Button.IsConnected(BaseButton.SignalName.Toggled, ToggledCallback))
        {
            content.Button.Disconnect(BaseButton.SignalName.Toggled, ToggledCallback);
        }
        if (content.Labels.IsConnected(Control.SignalName.MinimumSizeChanged, MinimumSizeChangedCallback))
        {
            content.Labels.Disconnect(Control.SignalName.MinimumSizeChanged, MinimumSizeChangedCallback);
        }
    }

    public override Vector2 _GetMinimumSize()
    {
        return MeasureMinimumSize();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationSortChildren && _content is { } content)
        {
            FitChildInRect(content.Button, new Rect2(Vector2.Zero, Size));
            var indicator = IndicatorSize();
            var inset = indicator.X + Tokens.Space2;
            var height = content.Labels.GetCombinedMinimumSize().Y;
            content.Labels.Position = new Vector2(IsSwitch ? 0 : inset, (Size.Y - height) * 0.5f);
            content.Labels.Size = new Vector2(Mathf.Max(0, Size.X - inset), height);
        }
    }

    private void OnNativeToggled(bool on)
    {
        _selected = on;
        EmitSignal(SignalName.Toggled, on);
    }

    private void UpdateLayout()
    {
        UpdateMinimumSize();
        QueueSort();
    }

    private Vector2 MeasureMinimumSize()
    {
        var content = _content?.Labels.GetCombinedMinimumSize() ?? Vector2.Zero;
        var indicator = IndicatorSize();
        return new Vector2(
            content.X + indicator.X + Tokens.Space2,
            Mathf.Max(content.Y, indicator.Y));
    }

    private void ApplyContent()
    {
        if (_content is not { } content)
        {
            return;
        }

        content.Label.Text = LabelText;
        content.Help.Text = Subtext;
        content.Help.Visible = !string.IsNullOrWhiteSpace(Subtext);
        content.Button.TooltipText = string.IsNullOrWhiteSpace(Subtext) ? LabelText : $"{LabelText}\n{Subtext}";
        UpdateLayout();
    }

    private void ApplyTheme()
    {
        if (_content is not { } content)
        {
            return;
        }

        content.Button.Theme = UiChoiceTheme.Create(Tokens, IsSwitch);
        Tokens.ApplyTextStyle(content.Label, Tokens.SmallStrongText);
        Tokens.ApplyTextStyle(content.Help, Tokens.NoteText);
        content.Label.AddThemeColorOverride("font_color", Tokens.Ink);
        content.Help.AddThemeColorOverride("font_color", Tokens.Muted);
        UpdateLayout();
    }

    private void ApplyState()
    {
        if (_content is not { } content)
        {
            return;
        }

        content.Button.Disabled = Disabled;
        content.Button.SetPressedNoSignal(_selected);
        content.Labels.Modulate = new Color(1, 1, 1, Disabled ? UiChoiceStyle.DisabledOpacity : 1);
    }

    private Vector2 IndicatorSize()
    {
        if (_content is not { } content)
        {
            return UiChoiceStyle.IndicatorSize(Tokens, IsSwitch);
        }

        return content.Button.GetThemeIcon("checked").GetSize()
            .Max(content.Button.GetThemeIcon("unchecked").GetSize());
    }
}
