using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Canonical row for build parts: icon, name, count, and four reference states.</summary>
[Tool]
[GlobalClass]
public partial class UiPartRow : Control
{
    [Signal]
    public delegate void PartSelectedEventHandler();

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
    private string _valueText = "";
    private PartRowState _state;
    private StyleBoxFlat? _style;

    [Export]
    public UiIconId IconId
    {
        get => _iconId;
        set
        {
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
            _label = value;
            Rebuild();
        }
    }

    [Export]
    public string ValueText
    {
        get => _valueText;
        set
        {
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
            _state = value;
            Rebuild();
        }
    }

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.None;
        MouseFilter = MouseFilterEnum.Pass;
        Rebuild();
    }

    private readonly UiUnsavedState _unsaved = new(CanvasItem.PropertyName.SelfModulate);

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

    public override void _GuiInput(InputEvent @event)
    {
        var activated = @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left };
        if (IsAvailable && activated)
        {
            EmitSignal(SignalName.PartSelected);
            AcceptEvent();
        }
    }

    public override Vector2 _GetMinimumSize() =>
        new(0, UiSize.Control.Default);

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

        SelfModulate = State is PartRowState.Locked or PartRowState.NoneLeft
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

        var label = UiFieldAndRows.Label(Label, UiTokens.Typography.SmallStrong, UiTokens.Color.Ink);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(label);

        if (State == PartRowState.Locked)
        {
            if (!string.IsNullOrWhiteSpace(ValueText))
            {
                row.AddChild(CreateTrailingLabel(ValueText));
            }

            row.AddChild(UiFieldAndRows.Icon(UiIconId.Lock, UiIconSize.Standard, UiThemeLookup.Color(this, UiTokens.Color.Ink)));
        }
        else
        {
            row.AddChild(CreateTrailingLabel(ValueText));
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

    private Label CreateTrailingLabel(string text)
    {
        var label = UiFieldAndRows.Label(text, UiTokens.Typography.ReadoutMedium, UiTokens.Color.Ink, HorizontalAlignment.Right);
        label.CustomMinimumSize = new Vector2(UiLayout.ColumnSmallWidth, 0);
        label.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        return label;
    }
}
