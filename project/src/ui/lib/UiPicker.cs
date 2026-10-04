using Godot;

namespace NodeRunner.Ui.Lib;

public readonly record struct UiPickerOption(
    string Label,
    UiIconId? Icon = null,
    UiMenuActionItem.MenuItemKind Kind = UiMenuActionItem.MenuItemKind.Default,
    string? Note = null,
    Color? IconTint = null);

/// <summary>Picker row that opens a menu-backed list of valid choices.</summary>
[Tool]
[GlobalClass]
public partial class UiPicker : PanelContainer
{
    public enum PickerState
    {
        Collapsed,
        Expanded,
        Locked,
    }

    [Signal]
    public delegate void SelectionChangedEventHandler(int selectedIndex);

    [Signal]
    public delegate void StateChangedEventHandler(PickerState state);

    private string _labelText = "Fixed part";
    private PickerState _state = PickerState.Collapsed;
    private bool _disabled;
    private string _belowText = string.Empty;
    private int _selectedIndex;
    private UiPickerOption[] _options =
    [
        new("Left thigh", UiIconId.Beam),
        new("Left shin", UiIconId.Beam, Note: "swaps"),
        new("Tail"),
    ];
    private UiMenu? _openMenu;

    [Export]
    public string LabelText
    {
        get => _labelText;
        set
        {
            _labelText = value;
            Rebuild();
        }
    }

    [Export]
    public PickerState State
    {
        get => _state;
        set => SetState(value, emit: false);
    }

    public string ValueText => SelectedOption?.Label ?? string.Empty;

    [Export]
    public bool Disabled
    {
        get => _disabled;
        set
        {
            _disabled = value;
            if (_disabled && IsExpanded)
            {
                _state = PickerState.Collapsed;
            }

            Rebuild();
        }
    }

    [Export]
    public string BelowText
    {
        get => _belowText;
        set
        {
            _belowText = value;
            Rebuild();
        }
    }

    [Export]
    public int SelectedIndex
    {
        get => EffectiveSelectedIndex;
        set
        {
            _selectedIndex = NormalizeSelectedIndex(value);
            Rebuild();
        }
    }

    public UiPickerOption[] Options
    {
        get => _options;
        set
        {
            _options = value ?? [];
            _selectedIndex = NormalizeSelectedIndex(_selectedIndex);
            Rebuild();
        }
    }

    public override void _Ready()
    {
        Rebuild();
    }

    private readonly UiUnsavedState _unsaved = new("theme_override_styles/panel");

    public override void _Notification(int what)
    {
        if (_unsaved.Handle(this, what, Rebuild))
        {
            return;
        }

        if (what == NotificationThemeChanged && IsNodeReady())
        {
            UiThemeRefresh.Guarded(this, Rebuild);
        }
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
        _openMenu = null;

        AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        MouseFilter = MouseFilterEnum.Pass;
        Modulate = Disabled ? Colors.White.ScaleAlpha(0.5f) : Colors.White;

        var stack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
        };
        stack.AddThemeConstantOverride("separation", (int)UiSize.Space.S1);
        AddChild(stack);

        stack.AddChild(UiFieldAndRows.Label(LabelText, UiTokens.Typography.Overline, UiTokens.Color.Muted));
        var closedRow = CreateClosedRow();
        stack.AddChild(closedRow);

        if (IsExpanded)
        {
            var menu = CreateOptionsMenu();
            var overlay = new UiLevelLayer();
            AddChild(overlay);
            overlay.AddChild(menu);
            _openMenu = menu;
            menu.Follow(closedRow, new Vector2(0, 1), new Vector2(0, UiSize.Space.S1));
            menu.CallDeferred(CanvasItem.MethodName.Show);
        }

        if (!string.IsNullOrWhiteSpace(BelowText))
        {
            var below = UiFieldAndRows.Label(BelowText, UiTokens.Typography.Note, UiTokens.Color.Muted);
            below.CustomMinimumSize = new Vector2(0, UiThemeLookup.FontSize(this, UiTokens.Typography.Note) + UiSize.Widget.SmallTextLeading);
            below.ClipText = true;
            stack.AddChild(below);
        }

    }

    private Button CreateClosedRow()
    {
        var rowButton = new Button
        {
            Disabled = IsLocked || Disabled || Options.Length == 0,
            CustomMinimumSize = new Vector2(UiLayout.SidePanelWidth, UiSize.Control.Small),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            TooltipText = LabelText,
        };
        rowButton.Pressed += () => SetState(IsExpanded ? PickerState.Collapsed : PickerState.Expanded, emit: true);
        // The one press look (#325); the row's content is its children and draws over the tint.
        rowButton.Draw += () =>
        {
            if (UiPressFeedback.Shows(rowButton, selected: false))
            {
                UiPressFeedback.Draw(rowButton, UiCorners.Uniform(UiSize.Radius.Medium), UiTokens.Color.PanelRaised, danger: false);
            }
        };
        rowButton.AddThemeStyleboxOverride("normal", ClosedRowStyle());
        rowButton.AddThemeStyleboxOverride("hover", ClosedRowStyle());
        rowButton.AddThemeStyleboxOverride("pressed", ClosedRowStyle());
        rowButton.AddThemeStyleboxOverride("hover_pressed", ClosedRowStyle());
        rowButton.AddThemeStyleboxOverride("focus", ClosedRowStyle(focused: true));
        rowButton.AddThemeStyleboxOverride("disabled", ClosedRowStyle(disabled: true));
        if (IsLocked)
        {
            var border = new DashedBorderOverlay
            {
                Color = UiThemeLookup.Color(this, UiTokens.Color.Accent),
            };
            border.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            rowButton.AddChild(border);
        }

        var margin = new MarginContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
        };
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", (int)UiSize.Space.S2);
        margin.AddThemeConstantOverride("margin_right", (int)UiSize.Space.S2);
        rowButton.AddChild(margin);

        var row = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        row.AddThemeConstantOverride("separation", (int)UiSize.Space.S2);
        margin.AddChild(row);

        var selected = SelectedOption;
        if (selected?.Icon is { } accessory)
        {
            row.AddChild(UiFieldAndRows.Icon(accessory, UiIconSize.Standard, ResolveIconTint(selected.Value, selected: true)));
        }

        var value = UiFieldAndRows.Label(ValueText, UiTokens.Typography.SmallStrong, ValueColor);
        value.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(value);
        if (TrailingIcon is { } trailingIcon)
        {
            row.AddChild(UiFieldAndRows.Icon(trailingIcon, UiIconSize.Standard, TrailingColor));
        }
        return rowButton;
    }

    private UiMenu CreateOptionsMenu()
    {
        var menu = new UiMenu
        {
            Width = UiLayout.SidePanelWidth,
            WidthMode = UiMenu.MenuWidthMode.Fixed,
            Compact = true,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            Visible = true,
        };
        var items = Options.Select((option, index) =>
        {
            return new UiMenuItemSpec(
                option.Label,
                option.Icon,
                option.Kind,
                option.Note,
                ResolveIconTint(option, index == EffectiveSelectedIndex),
                Selected: index == EffectiveSelectedIndex);
        }).ToArray();
        UiMenuItems.Populate(
            menu,
            items,
            showSelectedIndicator: true);
        menu.IndexClicked += index =>
        {
            if (index >= 0 && index < Options.Length)
            {
                Select(index);
                menu.Hide();
            }
        };
        return menu;
    }

    private StyleBoxFlat ClosedRowStyle(bool focused = false, bool disabled = false)
    {
        var border = IsLocked ? Colors.Transparent : UiThemeLookup.Color(this, UiTokens.Color.LineStrong);
        var style = UiThemeLookup.CreateStyleBox(UiThemeLookup.Color(this, UiTokens.Color.PanelRaised),
            border,
            IsLocked ? 0 : UiSize.Stroke.Hair,
            UiSize.Radius.Medium,
            horizontalPadding: 0,
            verticalPadding: 0);

        if (focused && !IsLocked && !Disabled)
        {
            style.BorderColor = UiThemeLookup.Color(this, UiTokens.Color.Accent);
        }

        return style;
    }

    private void SetState(PickerState state, bool emit)
    {
        var nextState = state == PickerState.Expanded && Disabled ? PickerState.Collapsed : state;
        if (_state == nextState)
        {
            return;
        }

        _state = nextState;
        Rebuild();
        if (emit)
        {
            EmitSignal(SignalName.StateChanged, (int)_state);
        }
    }

    private void Select(int index)
    {
        if (index < 0 || index >= Options.Length)
        {
            GD.PushError($"Invalid picker option index: {index}.");
            return;
        }

        _selectedIndex = index;
        _state = PickerState.Collapsed;
        Rebuild();
        EmitSignal(SignalName.StateChanged, (int)_state);
        EmitSignal(SignalName.SelectionChanged, _selectedIndex);
    }

    private UiPickerOption? SelectedOption =>
        EffectiveSelectedIndex >= 0 ? Options[EffectiveSelectedIndex] : null;

    private int EffectiveSelectedIndex => NormalizeSelectedIndex(_selectedIndex);

    private int NormalizeSelectedIndex(int index) =>
        Options.Length == 0 ? -1 : Mathf.Clamp(index, 0, Options.Length - 1);

    private bool IsExpanded => State == PickerState.Expanded;

    private bool IsLocked => State == PickerState.Locked;

    private Color ResolveIconTint(UiPickerOption option, bool selected) =>
        option.IconTint ?? (selected ? UiThemeLookup.Color(this, UiTokens.Color.Halo) : UiThemeLookup.Color(this, UiTokens.Color.Accent));

    private UiTokens.Color ValueColor => IsLocked ? UiTokens.Color.Muted : UiTokens.Color.Ink;

    private Color TrailingColor => IsLocked ? UiThemeLookup.Color(this, UiTokens.Color.Muted) : UiThemeLookup.Color(this, UiTokens.Color.Accent);

    private UiIconId? TrailingIcon => IsLocked ? UiIconId.Lock : Disabled ? null : IsExpanded ? UiIconId.ChevronDown : UiIconId.ChevronRight;

    private sealed partial class DashedBorderOverlay : Control
    {
        public Color Color { get; init; } = Colors.White;

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Draw()
        {
            var halfStroke = UiSize.Stroke.Hair * 0.5f;
            var rect = new Rect2(
                halfStroke,
                halfStroke,
                Mathf.Max(0, Size.X - UiSize.Stroke.Hair),
                Mathf.Max(0, Size.Y - UiSize.Stroke.Hair));
            UiDashedBorder.DrawRoundedRect(this, rect, UiSize.Radius.Medium, Color, UiSize.Stroke.Hair);
        }
    }
}
