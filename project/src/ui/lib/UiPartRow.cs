using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Canonical row for build parts: icon, name, count, and four reference states. A locked row shows
/// only its lock; its owner explains the lock once (#374), and may answer a tap on it
/// (<see cref="LockedPressedEventHandler"/>, #896). <see cref="Compact"/> rows are
/// <c>control-sm</c> high, as in the Build Parts tray.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiPartRow : Control
{
    [Signal]
    public delegate void PartSelectedEventHandler();

    [Signal]
    public delegate void LockedPressedEventHandler();

    public enum PartRowState
    {
        Rest,
        Selected,
        Locked,
        NoneLeft,
    }

    private const float _unavailableOpacity = 0.55f;

    private UiIconId _iconId = UiIconId.None;
    private string _label = "";
    private Func<string>? _labelSource;
    private Label? _nameLabel;
    private string _valueText = "";
    private PartRowState _state;
    private bool _compact;
    private StyleBoxFlat? _style;

    [Export]
    public UiIconId IconId
    {
        get => _iconId;
        set
        {
            if (_iconId == value)
            {
                return;
            }

            _iconId = value;
            Rebuild();
        }
    }

    [Export]
    public string Label
    {
        get => _label;
        set
        {
            if (_label == value)
            {
                return;
            }

            _label = value;
            Rebuild();
        }
    }

    /// <summary>
    /// Already translated name shown instead of <see cref="Label"/>; asked again when the language
    /// changes, with the name label's own auto-translation off meanwhile. Null shows Label.
    /// </summary>
    public Func<string>? LabelSource
    {
        get => _labelSource;
        set
        {
            _labelSource = value;
            Rebuild();
        }
    }

    [Export]
    public string ValueText
    {
        get => _valueText;
        set
        {
            if (_valueText == value)
            {
                return;
            }

            _valueText = value;
            Rebuild();
        }
    }

    [Export]
    public PartRowState State
    {
        get => _state;
        set
        {
            if (_state == value)
            {
                return;
            }

            _state = value;
            Rebuild();
        }
    }

    [Export]
    public bool Compact
    {
        get => _compact;
        set
        {
            if (_compact == value)
            {
                return;
            }

            _compact = value;
            Rebuild();
        }
    }

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.None;
        MouseFilter = MouseFilterEnum.Pass;
        Rebuild();
    }

    private readonly UiUnsavedState _unsaved = new(CanvasItem.PropertyName.Modulate);

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
        else if (what == NotificationTranslationChanged && _labelSource is not null && IsInstanceValid(_nameLabel))
        {
            // In place: a language change is propagated as a notification, which may not add children.
            _nameLabel!.Text = _labelSource();
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        var activated = @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left };
        if (IsAvailable && activated)
        {
            EmitSignal(SignalName.PartSelected);
            AcceptEvent();
        }
        else if (State == PartRowState.Locked && activated)
        {
            EmitSignal(SignalName.LockedPressed);
            AcceptEvent();
        }
    }

    public override Vector2 _GetMinimumSize() =>
        new(0, Compact ? UiSize.Control.Small : UiSize.Control.Default);

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

        _style = UiThemeLookup.CreateStyleBox(State == PartRowState.Selected
                ? UiThemeLookup.Color(this, UiTokens.Color.Accent).WithAlpha(UiThemeLookup.Alpha(this, UiTokens.Alpha.Soft))
                : UiThemeLookup.Color(this, UiTokens.Color.PanelRaised),
            State == PartRowState.Selected ? UiThemeLookup.Color(this, UiTokens.Color.Accent) : UiThemeLookup.Color(this, UiTokens.Color.LineStrong),
            State == PartRowState.Selected ? UiSize.Stroke.Signal : UiSize.Stroke.Hair,
            UiSize.Radius.Medium);

        if (State == PartRowState.Locked)
        {
            _style.BorderWidthLeft = 0;
            _style.BorderWidthTop = 0;
            _style.BorderWidthRight = 0;
            _style.BorderWidthBottom = 0;
        }

        Modulate = State is PartRowState.Locked or PartRowState.NoneLeft
            ? Colors.White with { A = _unavailableOpacity }
            : Colors.White;
        UpdateMinimumSize();
        QueueRedraw();

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", (int)UiSize.Space.S2);
        margin.AddThemeConstantOverride("margin_right", (int)UiSize.Space.S2);
        AddChild(margin);

        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.Fill,
        };
        row.AddThemeConstantOverride("separation", (int)UiSize.Space.S2);
        margin.AddChild(row);

        var iconTint = State == PartRowState.Selected ? UiThemeLookup.Color(this, UiTokens.Color.Accent) : UiThemeLookup.Color(this, UiTokens.Color.Ink);
        row.AddChild(UiIcons.Create(IconId, UiIconSize.Large, iconTint));

        var label = UiFieldAndRows.Label(LabelSource?.Invoke() ?? Label, UiTokens.Typography.SmallStrong, UiTokens.Color.Ink);
        UiTranslation.ShareContext(this, label);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (LabelSource is not null)
        {
            label.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        }

        row.AddChild(label);
        _nameLabel = label;

        if (State == PartRowState.Locked)
        {
            row.AddChild(UiFieldAndRows.Icon(UiIconId.Lock, UiIconSize.Standard, UiThemeLookup.Color(this, UiTokens.Color.Ink)));
        }
        else if (!string.IsNullOrWhiteSpace(ValueText))
        {
            var value = CreateTrailingLabel(ValueText);
            UiTranslation.ShareContext(this, value);
            row.AddChild(value);
        }
    }

    public override void _Draw()
    {
        base._Draw();
        if (_style is not null)
        {
            DrawStyleBox(_style, new Rect2(Vector2.Zero, Size));
        }

        if (State != PartRowState.Locked)
        {
            return;
        }

        var stroke = UiSize.Stroke.Hair;
        var rect = new Rect2(
            new Vector2(stroke * 0.5f, stroke * 0.5f),
            new Vector2(Math.Max(0, Size.X - stroke), Math.Max(0, Size.Y - stroke)));
        UiDashedBorder.DrawRoundedRect(this, rect, Math.Max(0, UiSize.Radius.Medium - (stroke * 0.5f)), UiThemeLookup.Color(this, UiTokens.Color.LineStrong), stroke);
    }

    private bool IsAvailable => State is PartRowState.Rest or PartRowState.Selected;

    /// <summary>
    /// What follows the finger while this row's part is dragged out (#376): its glyph on a raised
    /// <c>accent</c> tile, centred above the touch point so the finger does not hide it.
    /// </summary>
    public Control CreateDragPreview()
    {
        var size = UiSize.Control.Touch;
        var tile = new PanelContainer
        {
            Size = new Vector2(size, size),
            Position = new Vector2(-size / 2f, -size - UiSize.Space.S5),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        tile.AddThemeStyleboxOverride("panel", UiThemeLookup.CreateStyleBox(
            UiThemeLookup.Color(this, UiTokens.Color.PanelRaised),
            UiThemeLookup.Color(this, UiTokens.Color.Accent),
            UiSize.Stroke.Signal,
            UiSize.Radius.Medium));
        var icon = UiIcons.Create(IconId, UiIconSize.Large, UiThemeLookup.Color(this, UiTokens.Color.Accent));
        icon.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        tile.AddChild(icon);

        // Godot puts the preview's origin at the finger; the tile hangs off it.
        var preview = new Control { MouseFilter = MouseFilterEnum.Ignore };
        preview.AddChild(tile);
        return preview;
    }

    private Label CreateTrailingLabel(string text)
    {
        var label = UiFieldAndRows.Label(text, UiTokens.Typography.ReadoutMedium, UiTokens.Color.Ink, HorizontalAlignment.Right);
        label.CustomMinimumSize = new Vector2(UiLayout.ColumnSmallWidth, 0);
        label.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        return label;
    }
}
