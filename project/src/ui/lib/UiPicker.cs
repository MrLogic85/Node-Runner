using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Picker row that opens a menu-backed list of valid choices.</summary>
public partial class UiPicker : PanelContainer
{
    public enum PickerState
    {
        Collapsed,
        Expanded,
        Locked,
    }

    public readonly record struct PickerOption(
        string Label,
        UiIconId? Icon = null,
        string? Note = null,
        Color? IconTint = null);

    [Signal]
    public delegate void SelectionChangedEventHandler(string value);

    [Signal]
    public delegate void StateChangedEventHandler(PickerState state);

    private UiTokens _tokens = UiTokens.Neon;
    private string _labelText = "Fixed part";
    private PickerState _state = PickerState.Collapsed;
    private bool _disabled;
    private string _belowText = string.Empty;
    private int _selectedIndex;
    private PickerOption[] _options =
    [
        new("Left thigh", UiIconId.Beam),
        new("Left shin", UiIconId.Beam, "swaps"),
        new("Tail"),
    ];
    private UiOverflowMenu? _openMenu;
    private Control? _menuAnchor;

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
        get => _selectedIndex;
        set
        {
            _selectedIndex = ResolveSelectedIndex(value);
            Rebuild();
        }
    }

    public PickerOption[] Options
    {
        get => _options;
        set
        {
            _options = value ?? [];
            _selectedIndex = ResolveSelectedIndex(_selectedIndex);
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
        SetProcess(false);
        Rebuild();
    }

    public override void _Process(double delta)
    {
        if (IsExpanded && _openMenu is not null && _menuAnchor is not null)
        {
            PositionMenu(_openMenu, _menuAnchor);
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
        _menuAnchor = null;

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
            menu.TopLevel = true;
            menu.ZIndex = 100;
            AddChild(menu);
            _openMenu = menu;
            _menuAnchor = closedRow;
            Callable.From(() => PositionMenu(menu, closedRow)).CallDeferred();
            menu.CallDeferred(CanvasItem.MethodName.Show);
        }

        if (!string.IsNullOrWhiteSpace(BelowText))
        {
            stack.AddChild(UiFieldAndRows.Label(BelowText, _tokens, _tokens.NoteText, _tokens.Muted));
        }

        SetProcess(IsExpanded);
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

    private void PositionMenu(UiOverflowMenu menu, Control anchor)
    {
        if (!IsInstanceValid(menu) || !IsInstanceValid(anchor))
        {
            return;
        }

        menu.GlobalPosition = anchor.GlobalPosition + new Vector2(0, anchor.Size.Y + _tokens.Space1);
    }

    private UiOverflowMenu CreateOptionsMenu()
    {
        var menu = new UiOverflowMenu
        {
            CloseOnSelect = true,
            RowSize = UiOverflowMenu.MenuRowSize.Compact,
            ShowSelectedCheck = true,
            Tokens = _tokens,
            Width = _tokens.SidePanelWidth,
            WidthMode = UiOverflowMenu.MenuWidthMode.Fixed,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            Visible = true,
        };
        menu.SetActions(Options.Select((option, index) =>
        {
            var selected = index == SelectedIndex;
            return new UiOverflowMenu.MenuAction(
                index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                option.Label,
                option.Icon,
                selected ? UiComponentContracts.SemanticState.Selected : UiComponentContracts.SemanticState.Neutral,
                option.Note,
                ResolveIconTint(option, selected));
        }).ToArray());
        menu.ActionSelected += Select;
        return menu;
    }

    private StyleBoxFlat ClosedRowStyle(bool focused = false, bool disabled = false)
    {
        var border = IsLocked ? Colors.Transparent : _tokens.LineStrong;
        var style = _tokens.ControlStyle(
            _tokens.PanelRaised,
            border,
            _tokens.StrokeHair,
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

    private void Select(string optionId)
    {
        if (!int.TryParse(optionId, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var index)
            || index < 0
            || index >= Options.Length)
        {
            GD.PushError($"Invalid picker option id: {optionId}.");
            return;
        }

        _selectedIndex = index;
        _state = PickerState.Collapsed;
        Rebuild();
        EmitSignal(SignalName.StateChanged, (int)_state);
        EmitSignal(SignalName.SelectionChanged, ValueText);
    }

    private int ResolveSelectedIndex(int index) =>
        Options.Length == 0 ? -1 : Mathf.Clamp(index, 0, Options.Length - 1);

    private PickerOption? SelectedOption =>
        SelectedIndex >= 0 && SelectedIndex < Options.Length ? Options[SelectedIndex] : null;

    private bool IsExpanded => State == PickerState.Expanded;

    private bool IsLocked => State == PickerState.Locked;

    private Color ResolveIconTint(PickerOption option, bool selected) =>
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
            const float dash = 4;
            const float gap = 3;
            var halfStroke = Tokens.StrokeHair * 0.5f;
            var rect = new Rect2(
                halfStroke,
                halfStroke,
                Mathf.Max(0, Size.X - Tokens.StrokeHair),
                Mathf.Max(0, Size.Y - Tokens.StrokeHair));
            DrawDashedRoundedRect(rect, Tokens.RadiusMedium, Color, dash, gap);
        }

        private void DrawDashedRoundedRect(Rect2 rect, float radius, Color color, float dashLength, float gapLength)
        {
            var perimeter = (2 * (rect.Size.X + rect.Size.Y - (4 * radius))) + (Mathf.Tau * radius);
            if (perimeter <= 0)
            {
                return;
            }

            var pattern = dashLength + gapLength;
            var dashCount = Mathf.Max(1, Mathf.RoundToInt(perimeter / pattern));
            var fittedPattern = perimeter / dashCount;
            var fittedDashLength = fittedPattern * (dashLength / pattern);
            for (var index = 0; index < dashCount; index++)
            {
                var start = index * fittedPattern;
                var end = Mathf.Min(start + fittedDashLength, perimeter);
                DrawRoundedRectSegment(rect, radius, start, end, color);
            }
        }

        private void DrawRoundedRectSegment(Rect2 rect, float radius, float start, float end, Color color)
        {
            const int pointCount = 5;
            var points = new Vector2[pointCount];
            for (var index = 0; index < pointCount; index++)
            {
                var distance = Mathf.Lerp(start, end, index / (float)(pointCount - 1));
                points[index] = PointOnRoundedRect(rect, radius, distance);
            }

            DrawPolyline(points, color, Tokens.StrokeHair, antialiased: true);
        }

        private static Vector2 PointOnRoundedRect(Rect2 rect, float radius, float distance)
        {
            var straightWidth = Mathf.Max(0, rect.Size.X - (2 * radius));
            var straightHeight = Mathf.Max(0, rect.Size.Y - (2 * radius));
            var topEnd = straightWidth;
            var topRightArcEnd = topEnd + (Mathf.Pi * radius / 2);
            var rightEnd = topRightArcEnd + straightHeight;
            var bottomRightArcEnd = rightEnd + (Mathf.Pi * radius / 2);
            var bottomEnd = bottomRightArcEnd + straightWidth;
            var bottomLeftArcEnd = bottomEnd + (Mathf.Pi * radius / 2);
            var leftEnd = bottomLeftArcEnd + straightHeight;
            var leftTopArcEnd = leftEnd + (Mathf.Pi * radius / 2);
            distance = Mathf.PosMod(distance, leftTopArcEnd);

            if (distance <= topEnd)
            {
                return new Vector2(rect.Position.X + radius + distance, rect.Position.Y);
            }

            if (distance <= topRightArcEnd)
            {
                var angle = -Mathf.Pi / 2 + ((distance - topEnd) / (Mathf.Pi * radius / 2) * Mathf.Pi / 2);
                var center = new Vector2(rect.End.X - radius, rect.Position.Y + radius);
                return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            if (distance <= rightEnd)
            {
                return new Vector2(rect.End.X, rect.Position.Y + radius + distance - topRightArcEnd);
            }

            if (distance <= bottomRightArcEnd)
            {
                var angle = (distance - rightEnd) / (Mathf.Pi * radius / 2) * Mathf.Pi / 2;
                var center = new Vector2(rect.End.X - radius, rect.End.Y - radius);
                return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            if (distance <= bottomEnd)
            {
                return new Vector2(rect.End.X - radius - (distance - bottomRightArcEnd), rect.End.Y);
            }

            if (distance <= bottomLeftArcEnd)
            {
                var angle = Mathf.Pi / 2 + ((distance - bottomEnd) / (Mathf.Pi * radius / 2) * Mathf.Pi / 2);
                var center = new Vector2(rect.Position.X + radius, rect.End.Y - radius);
                return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            if (distance <= leftEnd)
            {
                return new Vector2(rect.Position.X, rect.End.Y - radius - (distance - bottomLeftArcEnd));
            }

            var finalAngle = Mathf.Pi + ((distance - leftEnd) / (Mathf.Pi * radius / 2) * Mathf.Pi / 2);
            var finalCenter = new Vector2(rect.Position.X + radius, rect.Position.Y + radius);
            return finalCenter + new Vector2(Mathf.Cos(finalAngle), Mathf.Sin(finalAngle)) * radius;
        }
    }
}
