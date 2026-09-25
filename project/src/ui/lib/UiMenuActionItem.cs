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
        UiTokens tokens,
        bool showSelectedIndicator = false)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(specs);
        ArgumentNullException.ThrowIfNull(tokens);

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
                Tokens = tokens,
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
    private TextureRect? _selectedIndicator;
    private Callable PressedCallback => new(this, MethodName.Activate);

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
        ApplyTypography();
        _button.AddThemeConstantOverride("h_separation", (int)Tokens.Space2);

        if (IconId != UiIconId.None)
        {
            UiIcons.Apply(
                _button,
                IconId,
                SizeVariant == MenuItemSize.Compact ? UiIconSize.Standard : UiIconSize.Large,
                IconTint ?? IconColor());
            _button.AddThemeColorOverride("icon_disabled_color", Tokens.Muted);
        }
        else
        {
            _button.Icon = null;
        }

        RefreshNote();
        RefreshSelectedIndicator();
        foreach (var state in new[] { "normal", "pressed", "focus", "disabled" })
        {
            _button.AddThemeStyleboxOverride(state, CreateStyle(Colors.Transparent));
        }
        _button.AddThemeStyleboxOverride("hover", CreateStyle(Tokens.AccentSoft));
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
        RefreshItem();
    }

    private void DisconnectButton()
    {
        if (_button is not null
            && _button.IsConnected(BaseButton.SignalName.Pressed, PressedCallback))
        {
            _button.Disconnect(BaseButton.SignalName.Pressed, PressedCallback);
        }
    }

    private void Activate() => EmitSignal(SignalName.Activated);

    private void ApplyTypography()
    {
        if (_button is null)
        {
            return;
        }

        var style = SizeVariant == MenuItemSize.Compact
            ? Tokens.SmallStrongText
            : Tokens.BodyStrongText;
        var color = TextColor();
        var typography = new Godot.Theme();
        if (UiTokens.CreateTextFont(style) is { } font)
        {
            typography.SetFont("font", "Button", font);
        }
        typography.SetFontSize("font_size", "Button", (int)style.FontSize);
        typography.SetColor("font_color", "Button", color);
        typography.SetColor("font_focus_color", "Button", color);
        typography.SetColor("font_hover_color", "Button", Tokens.Ink);
        typography.SetColor("font_pressed_color", "Button", color);
        typography.SetColor("font_hover_pressed_color", "Button", color);
        typography.SetColor("font_disabled_color", "Button", Tokens.Muted);
        _button.Theme = typography;
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
        _noteLabel.CustomMinimumSize = new Vector2(Tokens.ControlSmall * 1.5f, 0);
        Tokens.ApplyTextStyle(_noteLabel, Tokens.NoteText);
        _noteLabel.AddThemeColorOverride("font_color", Tokens.Muted);
        _noteLabel.Size = _noteLabel.GetCombinedMinimumSize();
        _noteLabel.SetAnchorsPreset(LayoutPreset.CenterRight);

        var indicatorInset = SelectedIndicatorWidth;
        _noteLabel.OffsetLeft = -HorizontalPadding - indicatorInset - _noteLabel.Size.X;
        _noteLabel.OffsetRight = -HorizontalPadding - indicatorInset;
        _noteLabel.OffsetTop = _noteLabel.Size.Y * -0.5f;
        _noteLabel.OffsetBottom = _noteLabel.Size.Y * 0.5f;
        _noteLabel.Visible = true;
    }

    private void RefreshSelectedIndicator()
    {
        if (_button is null)
        {
            return;
        }

        var visible = ShowSelectedIndicator && Selected;
        if (!visible)
        {
            if (_selectedIndicator is not null)
            {
                _selectedIndicator.Visible = false;
            }
            return;
        }

        _selectedIndicator ??= _button.GetNodeOrNull<TextureRect>("SelectedIndicator");
        if (_selectedIndicator is null)
        {
            _selectedIndicator = UiIcons.Create(UiIconId.Check, UiIconSize.Small, Tokens.Accent);
            _selectedIndicator.Name = "SelectedIndicator";
            _button.AddChild(_selectedIndicator, false, InternalMode.Front);
        }

        var pixels = UiIcons.Pixels(UiIconSize.Small);
        _selectedIndicator.Texture = UiIcons.Load(UiIconId.Check, UiIconSize.Small);
        _selectedIndicator.SelfModulate = Disabled ? Tokens.Muted : Tokens.Accent;
        _selectedIndicator.SetAnchorsPreset(LayoutPreset.CenterRight);
        _selectedIndicator.OffsetLeft = -HorizontalPadding - pixels;
        _selectedIndicator.OffsetRight = -HorizontalPadding;
        _selectedIndicator.OffsetTop = pixels * -0.5f;
        _selectedIndicator.OffsetBottom = pixels * 0.5f;
        _selectedIndicator.Visible = true;
    }

    private Color TextColor() =>
        Kind == MenuItemKind.Danger ? Tokens.Danger : Tokens.Ink;

    private Color IconColor() =>
        Kind == MenuItemKind.Danger ? Tokens.Danger : Tokens.Accent;

    private float NoteWidth =>
        string.IsNullOrWhiteSpace(NoteText) || _noteLabel is null
            ? 0
            : _noteLabel.Size.X + Tokens.Space2;

    private float SelectedIndicatorWidth =>
        ShowSelectedIndicator && Selected
            ? Tokens.Space2 + UiIcons.Pixels(UiIconSize.Small)
            : 0;

    private StyleBoxFlat CreateStyle(Color background) =>
        new()
        {
            BgColor = background,
            ContentMarginLeft = HorizontalPadding,
            ContentMarginRight = HorizontalPadding + NoteWidth + SelectedIndicatorWidth,
        };
}
