using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Standard optional action row for UiMenu: an icon, the label with an optional note under it,
/// and a check for the selected choice. Containers lay the row out; a transparent Button behind
/// them only handles press, hover and focus.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiMenuActionItem : UiMenuItem, ISerializationListener
{
    public enum MenuItemKind
    {
        Default,
        Danger,
    }

    [Signal]
    public delegate void ActivatedEventHandler();

    private string _labelText = "";
    private string _noteText = string.Empty;
    private UiIconId _iconId = UiIconId.None;
    private MenuItemKind _kind;
    private bool _showSelectedIndicator;
    private Color? _iconTint;
    private Button? _button;
    private MarginContainer? _content;
    private TextureRect? _icon;
    private Label? _label;
    private Label? _note;
    private TextureRect? _check;
    private Callable PressedCallback => new(this, MethodName.Activate);
    private Callable RedrawCallback => new(this, CanvasItem.MethodName.QueueRedraw);

    // A held press leaves the Pressed draw mode when the pointer slides off the row.
    private static readonly StringName[] _redrawSignals =
    [
        BaseButton.SignalName.ButtonDown,
        BaseButton.SignalName.ButtonUp,
        Control.SignalName.MouseEntered,
        Control.SignalName.MouseExited,
    ];

    [Export]
    public string LabelText
    {
        get => _labelText;
        set
        {
            _labelText = value ?? string.Empty;
            RefreshItem();
        }
    }

    [Export]
    public string NoteText
    {
        get => _noteText;
        set
        {
            _noteText = value ?? string.Empty;
            RefreshItem();
        }
    }

    [Export]
    public UiIconId IconId
    {
        get => _iconId;
        set
        {
            _iconId = value;
            RefreshItem();
        }
    }

    [Export]
    public MenuItemKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            RefreshItem();
        }
    }

    [Export]
    public bool ShowSelectedIndicator
    {
        get => _showSelectedIndicator;
        set
        {
            _showSelectedIndicator = value;
            RefreshItem();
        }
    }

    public Color? IconTint
    {
        get => _iconTint;
        set
        {
            _iconTint = value;
            RefreshItem();
        }
    }

    public override void _EnterTree() => RequestReady();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        Build();
    }

    public override void _ExitTree() => DisconnectButton();

    public void OnBeforeSerialize() => DisconnectButton();

    public void OnAfterDeserialize() => CallDeferred(MethodName.RestoreContent);

    public override Vector2 _GetMinimumSize()
    {
        var content = _content?.GetCombinedMinimumSize() ?? Vector2.Zero;
        return new Vector2(content.X, Mathf.Max(RowHeight, content.Y));
    }

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationSortChildren && _button is not null && _content is not null)
        {
            var rect = new Rect2(Vector2.Zero, Size);
            FitChildInRect(_button, rect);
            FitChildInRect(_content, rect);
        }
        else if (what == NotificationThemeChanged)
        {
            RefreshIconColors();
        }
    }

    protected override void RefreshItem()
    {
        if (_button is null || _content is null || _icon is null || _label is null || _note is null || _check is null)
        {
            return;
        }

        _button.Disabled = Disabled;
        foreach (var side in new[] { "margin_left", "margin_right" })
        {
            _content.AddThemeConstantOverride(side, (int)HorizontalPadding);
        }

        var iconPixels = UiIcons.Pixels(IconSize);
        _icon.Visible = IconId != UiIconId.None;
        _icon.Texture = _icon.Visible ? UiIcons.Load(IconId, IconSize) : null;
        _icon.CustomMinimumSize = new Vector2(iconPixels, iconPixels);

        _label.Text = LabelText;
        _label.ThemeTypeVariation = UiTokens.Variation(
            SizeVariant == MenuItemSize.Compact ? UiTokens.Typography.SmallStrong : UiTokens.Typography.BodyStrong,
            Disabled ? UiTokens.Color.Muted : Kind == MenuItemKind.Danger ? UiTokens.Color.Danger : UiTokens.Color.Ink);

        _note.Text = NoteText;
        _note.Visible = !string.IsNullOrWhiteSpace(NoteText);
        _note.ThemeTypeVariation = UiTokens.Variation(UiTokens.Typography.Note, UiTokens.Color.Muted);

        var checkPixels = UiIcons.Pixels(UiIconSize.Small);
        _check.Visible = ShowSelectedIndicator && Selected;
        _check.CustomMinimumSize = new Vector2(checkPixels, checkPixels);

        RefreshIconColors();
        QueueRedraw();
        RefreshLayout();
    }

    public override void _Draw()
    {
        if (_button is { } button && UiPressFeedback.Shows(button, Selected))
        {
            UiPressFeedback.Draw(this, UiCorners.Uniform(0), UiTokens.Color.Panel, Kind == MenuItemKind.Danger);
        }

        base._Draw();
    }

    private UiIconSize IconSize =>
        SizeVariant == MenuItemSize.Compact ? UiIconSize.Standard : UiIconSize.Large;

    private void RefreshIconColors()
    {
        if (_icon is null || _check is null)
        {
            return;
        }

        var iconToken = Disabled ? UiTokens.Color.Muted : Kind == MenuItemKind.Danger ? UiTokens.Color.Danger : UiTokens.Color.Accent;
        _icon.SelfModulate = IconTint ?? UiThemeLookup.Color(this, iconToken);
        _check.SelfModulate = UiThemeLookup.Color(this, Disabled ? UiTokens.Color.Muted : UiTokens.Color.Accent);
    }

    private void RestoreContent()
    {
        if (IsInsideTree())
        {
            Build();
        }
    }

    private void Build()
    {
        DisconnectButton();
        var own = GetChildren();
        foreach (var child in GetChildren(includeInternal: true).Where(child => !own.Contains(child)))
        {
            RemoveChild(child);
            child.QueueFree();
        }

        _button = new Button
        {
            Name = "Button",
            MouseFilter = MouseFilterEnum.Pass,
        };
        var empty = new StyleBoxEmpty();
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" })
        {
            _button.AddThemeStyleboxOverride(state, empty);
        }
        AddChild(_button, false, InternalMode.Front);

        _icon = new TextureRect
        {
            Name = "Icon",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = UiIcons.IconFilter,
        };
        _label = new Label { Name = "Label" };
        _note = new Label { Name = "Note" };
        var text = new VBoxContainer
        {
            Name = "Text",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        text.AddThemeConstantOverride("separation", 0);
        text.AddChild(_label);
        text.AddChild(_note);
        _check = new TextureRect
        {
            Name = "Check",
            Texture = UiIcons.Load(UiIconId.Check, UiIconSize.Small),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = UiIcons.IconFilter,
        };
        var row = new HBoxContainer { Name = "Row", MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", (int)UiSize.Space.S2);
        row.AddChild(_icon);
        row.AddChild(text);
        row.AddChild(_check);
        _content = new MarginContainer { Name = "Content", MouseFilter = MouseFilterEnum.Ignore };
        _content.AddChild(row);
        AddChild(_content, false, InternalMode.Front);

        _button.Connect(BaseButton.SignalName.Pressed, PressedCallback);
        foreach (var signal in _redrawSignals)
        {
            _button.Connect(signal, RedrawCallback);
        }
        RefreshItem();
    }

    private void DisconnectButton()
    {
        if (_button is null || !IsInstanceValid(_button))
        {
            return;
        }

        if (_button.IsConnected(BaseButton.SignalName.Pressed, PressedCallback))
        {
            _button.Disconnect(BaseButton.SignalName.Pressed, PressedCallback);
        }
        foreach (var signal in _redrawSignals)
        {
            if (_button.IsConnected(signal, RedrawCallback))
            {
                _button.Disconnect(signal, RedrawCallback);
            }
        }
    }

    private void Activate() => EmitSignal(SignalName.Activated);
}
