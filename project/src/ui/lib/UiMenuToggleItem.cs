using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>A menu row containing the standard toggle control.</summary>
[Tool]
[GlobalClass]
public partial class UiMenuToggleItem : UiMenuItem, ISerializationListener
{
    [Signal]
    public delegate void ToggledEventHandler(bool on);

    private string _labelText = "Sounds";
    private string _subtext = string.Empty;
    private bool _on = true;
    private UiToggleRow? _toggle;
    private Callable ToggledCallback => new(this, MethodName.OnToggled);

    [Export]
    public string LabelText
    {
        get => _labelText;
        set
        {
            _labelText = value ?? string.Empty;
            ApplyContent();
        }
    }

    [Export]
    public string Subtext
    {
        get => _subtext;
        set
        {
            _subtext = value ?? string.Empty;
            ApplyContent();
        }
    }

    [Export]
    public bool On
    {
        get => _toggle?.On ?? _on;
        set
        {
            _on = value;
            if (_toggle is not null)
            {
                _toggle.On = value;
            }
        }
    }

    public override void _EnterTree() => RequestReady();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        InitializeToggle();
    }

    public override void _ExitTree() => DisconnectToggle();

    public void OnBeforeSerialize() => DisconnectToggle();

    public void OnAfterDeserialize() => CallDeferred(MethodName.RestoreToggle);

    public override Vector2 _GetMinimumSize()
    {
        var toggleSize = _toggle?.GetCombinedMinimumSize() ?? Vector2.Zero;
        return new Vector2(
            toggleSize.X + (HorizontalPadding * 2),
            Mathf.Max(RowHeight, toggleSize.Y));
    }

    public override void _Notification(int what)
    {
        if (what != NotificationSortChildren || _toggle is null)
        {
            return;
        }

        FitChildInRect(_toggle, new Rect2(
            HorizontalPadding,
            0,
            Mathf.Max(0, Size.X - (HorizontalPadding * 2)),
            Size.Y));
    }

    private void RestoreToggle()
    {
        if (IsInsideTree())
        {
            InitializeToggle();
        }
    }

    private void InitializeToggle()
    {
        DisconnectToggle();
        _toggle = GetChildren(includeInternal: true).OfType<UiToggleRow>().FirstOrDefault();
        if (_toggle is null)
        {
            _toggle = new UiToggleRow { Name = "Toggle" };
            AddChild(_toggle, false, InternalMode.Front);
        }

        if (!_toggle.IsConnected(UiChoiceRow.SignalName.Toggled, ToggledCallback))
        {
            _toggle.Connect(UiChoiceRow.SignalName.Toggled, ToggledCallback);
        }

        ApplyContent();
    }

    private void DisconnectToggle()
    {
        if (_toggle is not null
            && _toggle.IsConnected(UiChoiceRow.SignalName.Toggled, ToggledCallback))
        {
            _toggle.Disconnect(UiChoiceRow.SignalName.Toggled, ToggledCallback);
        }
    }

    private void ApplyContent()
    {
        if (_toggle is null)
        {
            return;
        }

        _toggle.LabelText = LabelText;
        _toggle.Subtext = Subtext;
        _toggle.On = _on;
        _toggle.Disabled = Disabled;
        _toggle.Tokens = Tokens;
        UpdateMinimumSize();
        QueueSort();
    }

    protected override void RefreshItem() => ApplyContent();

    private void OnToggled(bool on)
    {
        _on = on;
        EmitSignal(SignalName.Toggled, on);
    }
}
