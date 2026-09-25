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

    private UiTokens _tokens = UiTokens.Neon;
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

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            Rebuild();
        }
    }

    public override void _Ready()
    {
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
        _openMenu = null;

        AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        MouseFilter = MouseFilterEnum.Pass;
        Modulate = Disabled ? UiTokens.MultiplyAlpha(Colors.White, 0.5f) : Colors.White;

        var stack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
        };
        stack.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        AddChild(stack);

        stack.AddChild(UiFieldAndRows.Label(LabelText, _tokens, _tokens.OverlineText, _tokens.Muted));
        var closedRow = CreateClosedRow();
        stack.AddChild(closedRow);

        if (IsExpanded)
        {
            var menu = CreateOptionsMenu();
            AddChild(menu);
            _openMenu = menu;
            menu.Follow(closedRow, new Vector2(0, 1), new Vector2(0, _tokens.Space1));
            menu.CallDeferred(CanvasItem.MethodName.Show);
        }

        if (!string.IsNullOrWhiteSpace(BelowText))
        {
            var below = UiFieldAndRows.Label(BelowText, _tokens, _tokens.NoteText, _tokens.Muted);
            below.CustomMinimumSize = new Vector2(0, _tokens.NoteText.LineHeight);
            below.ClipText = true;
            stack.AddChild(below);
        }

    }

    private Button CreateClosedRow()
    {
        var rowButton = new Button
        {
            Disabled = IsLocked || Disabled || Options.Length == 0,
            CustomMinimumSize = new Vector2(_tokens.SidePanelWidth, _tokens.ControlSmall),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            TooltipText = LabelText,
        };
        rowButton.Pressed += () => SetState(IsExpanded ? PickerState.Collapsed : PickerState.Expanded, emit: true);
        rowButton.AddThemeStyleboxOverride("normal", ClosedRowStyle());
        rowButton.AddThemeStyleboxOverride("hover", ClosedRowStyle(focused: true));
        rowButton.AddThemeStyleboxOverride("pressed", ClosedRowStyle(focused: true));
        rowButton.AddThemeStyleboxOverride("focus", ClosedRowStyle(focused: true));
        rowButton.AddThemeStyleboxOverride("disabled", ClosedRowStyle(disabled: true));
        if (IsLocked)
        {
            var border = new DashedBorderOverlay
            {
                Tokens = _tokens,
                Color = _tokens.Accent,
            };
            border.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            rowButton.AddChild(border);
        }

        var margin = new MarginContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
        };
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", (int)_tokens.Space2);
        margin.AddThemeConstantOverride("margin_right", (int)_tokens.Space2);
        rowButton.AddChild(margin);

        var row = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        row.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        margin.AddChild(row);

        var selected = SelectedOption;
        if (selected?.Icon is { } accessory)
        {
            row.AddChild(UiFieldAndRows.Icon(accessory, UiIconSize.Standard, ResolveIconTint(selected.Value, selected: true)));
        }

        var value = UiFieldAndRows.Label(ValueText, _tokens, _tokens.SmallStrongText, ValueColor);
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
            Tokens = _tokens,
            Width = _tokens.SidePanelWidth,
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
            _tokens,
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
        var border = IsLocked ? Colors.Transparent : _tokens.LineStrong;
        var style = _tokens.ControlStyle(
            _tokens.PanelRaised,
            border,
            IsLocked ? 0 : _tokens.StrokeHair,
            _tokens.RadiusMedium,
            horizontalPadding: 0,
            verticalPadding: 0);

        if (focused && !IsLocked && !Disabled)
        {
            style.BorderColor = _tokens.Accent;
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
        option.IconTint ?? (selected ? _tokens.Halo : _tokens.Accent);

    private Color ValueColor => IsLocked ? _tokens.Muted : _tokens.Ink;

    private Color TrailingColor => IsLocked ? _tokens.Muted : _tokens.Accent;

    private UiIconId? TrailingIcon => IsLocked ? UiIconId.Lock : Disabled ? null : IsExpanded ? UiIconId.ChevronDown : UiIconId.ChevronRight;

    private sealed partial class DashedBorderOverlay : Control
    {
        public UiTokens Tokens { get; init; } = UiTokens.Neon;

        public Color Color { get; init; } = Colors.White;

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Draw()
        {
            var halfStroke = Tokens.StrokeHair * 0.5f;
            var rect = new Rect2(
                halfStroke,
                halfStroke,
                Mathf.Max(0, Size.X - Tokens.StrokeHair),
                Mathf.Max(0, Size.Y - Tokens.StrokeHair));
            UiDashedBorder.DrawRoundedRect(this, rect, Tokens.RadiusMedium, Color, Tokens.StrokeHair);
        }
    }
}
