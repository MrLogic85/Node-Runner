using Godot;

namespace NodeRunner.Ui.Lib;

public readonly record struct UiMenuItemSpec(
    string Label,
    UiIconId? Icon = null,
    UiMenuActionItem.MenuItemKind Kind = UiMenuActionItem.MenuItemKind.Default,
    string? Note = null,
    Color? IconTint = null,
    bool Selected = false);

public static class UiMenuItems
{
    public static void Populate(
        UiMenu menu,
        IEnumerable<UiMenuItemSpec> specs,
        bool showSelectedIndicator = false)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(specs);

        foreach (var child in menu.GetChildren())
        {
            menu.RemoveChild(child);
            child.QueueFree();
        }

        foreach (var spec in specs)
        {
            menu.AddChild(new UiMenuActionItem
            {
                LabelText = spec.Label,
                NoteText = spec.Note ?? string.Empty,
                IconId = spec.Icon ?? UiIconId.None,
                Kind = spec.Kind,
                Selected = spec.Selected,
                ShowSelectedIndicator = showSelectedIndicator,
                IconTint = spec.IconTint,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            });
        }
    }
}

/// <summary>Standard optional action row for UiMenu.</summary>
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

    private string _labelText = "Menu item";
    private string _noteText = string.Empty;
    private UiIconId _iconId;
    private MenuItemKind _kind;
    private bool _showSelectedIndicator;
    private Button? _button;
    private Label? _noteLabel;
    private Callable PressedCallback => new(this, MethodName.Activate);
    private Callable RedrawCallback => new(this, CanvasItem.MethodName.QueueRedraw);

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

    public Color? IconTint { get; set; }

    public override void _EnterTree() => RequestReady();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        InitializeButton();
    }

    public override void _ExitTree() => DisconnectButton();

    public void OnBeforeSerialize() => DisconnectButton();

    public void OnAfterDeserialize() => CallDeferred(MethodName.RestoreButton);

    public override Vector2 _GetMinimumSize() =>
        _button?.GetCombinedMinimumSize() ?? new Vector2(0, RowHeight);

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationSortChildren && _button is not null)
        {
            FitChildInRect(_button, new Rect2(Vector2.Zero, Size));
        }
    }

    protected override void RefreshItem()
    {
        if (_button is null)
        {
            return;
        }

        _button.CustomMinimumSize = new Vector2(0, RowHeight);
        _button.Text = LabelText;
        _button.Disabled = Disabled;
        _button.Alignment = HorizontalAlignment.Left;
        _button.ThemeTypeVariation = UiThemeExpander.MenuItemButtonVariation(
            compact: SizeVariant == MenuItemSize.Compact,
            danger: Kind == MenuItemKind.Danger);
        ApplyIcon();
        RefreshNote();

        // Transparent and margin-only: hover and selection are drawn by this item so their
        // colors follow the inherited Theme without re-applying overrides.
        var style = CreateStyle();
        foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
        {
            _button.AddThemeStyleboxOverride(state, style);
        }
        QueueRedraw();
        RefreshLayout();
    }

    private void RestoreButton()
    {
        if (IsInsideTree())
        {
            InitializeButton();
        }
    }

    private void InitializeButton()
    {
        DisconnectButton();
        _button = GetChildren(includeInternal: true).OfType<Button>().FirstOrDefault();
        if (_button is null)
        {
            _button = new Button { Name = "Button" };
            AddChild(_button, false, InternalMode.Front);
        }

        _button.MouseFilter = MouseFilterEnum.Pass;
        if (!_button.IsConnected(BaseButton.SignalName.Pressed, PressedCallback))
        {
            _button.Connect(BaseButton.SignalName.Pressed, PressedCallback);
        }
        foreach (var signal in new[] { Control.SignalName.MouseEntered, Control.SignalName.MouseExited })
        {
            if (!_button.IsConnected(signal, RedrawCallback))
            {
                _button.Connect(signal, RedrawCallback);
            }
        }
        RefreshItem();
    }

    private void DisconnectButton()
    {
        if (_button is null)
        {
            return;
        }

        if (_button.IsConnected(BaseButton.SignalName.Pressed, PressedCallback))
        {
            _button.Disconnect(BaseButton.SignalName.Pressed, PressedCallback);
        }
        foreach (var signal in new[] { Control.SignalName.MouseEntered, Control.SignalName.MouseExited })
        {
            if (_button.IsConnected(signal, RedrawCallback))
            {
                _button.Disconnect(signal, RedrawCallback);
            }
        }
    }

    private void Activate() => EmitSignal(SignalName.Activated);

    private void ApplyIcon()
    {
        if (_button is null)
        {
            return;
        }

        if (IconId == UiIconId.None)
        {
            _button.Icon = null;
            return;
        }

        var size = SizeVariant == MenuItemSize.Compact ? UiIconSize.Standard : UiIconSize.Large;
        if (IconTint is { } tint)
        {
            UiIcons.Apply(_button, IconId, size, tint);
        }
        else
        {
            UiIcons.Apply(_button, IconId, size);
        }
    }

    public override void _Draw()
    {
        if (!Selected && !Disabled && _button?.IsHovered() == true)
        {
            DrawRect(new Rect2(Vector2.Zero, Size), UiThemeLookup.Color(this, UiTokens.Color.Accent).WithAlpha(UiThemeLookup.Alpha(this, UiTokens.Alpha.Soft)));
        }

        base._Draw();
        if (!ShowSelectedIndicator || !Selected)
        {
            return;
        }

        var pixels = UiIcons.Pixels(UiIconSize.Small);
        var rect = new Rect2(
            Size.X - HorizontalPadding - pixels,
            (Size.Y - pixels) * 0.5f,
            pixels,
            pixels);
        var color = UiThemeLookup.Color(this, Disabled ? UiTokens.Color.Muted : UiTokens.Color.Accent);
        DrawTextureRect(UiIcons.Load(UiIconId.Check, UiIconSize.Small), rect, false, color);
    }

    private void RefreshNote()
    {
        if (_button is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(NoteText))
        {
            if (_noteLabel is not null)
            {
                _noteLabel.Visible = false;
            }
            return;
        }

        _noteLabel ??= _button.GetNodeOrNull<Label>("Note");
        if (_noteLabel is null)
        {
            _noteLabel = new Label
            {
                Name = "Note",
                HorizontalAlignment = HorizontalAlignment.Right,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _button.AddChild(_noteLabel, false, InternalMode.Front);
        }

        _noteLabel.Text = NoteText;
        _noteLabel.CustomMinimumSize = new Vector2(UiSize.Control.Small * 1.5f, 0);
        _noteLabel.ThemeTypeVariation = UiTokens.Variation(UiTokens.Typography.Note, UiTokens.Color.Muted);
        _noteLabel.Size = _noteLabel.GetCombinedMinimumSize();
        _noteLabel.SetAnchorsPreset(LayoutPreset.CenterRight);

        var indicatorInset = SelectedIndicatorWidth;
        _noteLabel.OffsetLeft = -HorizontalPadding - indicatorInset - _noteLabel.Size.X;
        _noteLabel.OffsetRight = -HorizontalPadding - indicatorInset;
        _noteLabel.OffsetTop = _noteLabel.Size.Y * -0.5f;
        _noteLabel.OffsetBottom = _noteLabel.Size.Y * 0.5f;
        _noteLabel.Visible = true;
    }

    private float NoteWidth =>
        string.IsNullOrWhiteSpace(NoteText) || _noteLabel is null
            ? 0
            : _noteLabel.Size.X + UiSize.Space.S2;

    private float SelectedIndicatorWidth =>
        ShowSelectedIndicator && Selected
            ? UiSize.Space.S2 + UiIcons.Pixels(UiIconSize.Small)
            : 0;

    private StyleBoxEmpty CreateStyle() =>
        new()
        {
            ContentMarginLeft = HorizontalPadding,
            ContentMarginRight = HorizontalPadding + NoteWidth + SelectedIndicatorWidth,
        };
}
