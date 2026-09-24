using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Canonical row for build parts: icon, name, count, and four reference states.</summary>
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

    private UiTokens _tokens = UiTokens.Neon;
    private UiPartIconId _partIconId = UiPartIconId.Servo;
    private string _partName = "Servo";
    private string _countText = "3 left";
    private PartRowState _state;
    private StyleBoxFlat? _style;

    [Export]
    public UiPartIconId PartIconId
    {
        get => _partIconId;
        set
        {
            _partIconId = value;
            Rebuild();
        }
    }

    [Export]
    public string PartName
    {
        get => _partName;
        set
        {
            _partName = value;
            Rebuild();
        }
    }

    [Export]
    public string CountText
    {
        get => _countText;
        set
        {
            _countText = value;
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
        FocusMode = FocusModeEnum.None;
        MouseFilter = MouseFilterEnum.Pass;
        Rebuild();
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

        CustomMinimumSize = new Vector2(0, UiComponentContracts.PartRowTouchHeight);
        _style = _tokens.ControlStyle(
            State == PartRowState.Selected ? _tokens.AccentSoft : _tokens.PanelRaised,
            State == PartRowState.Selected ? _tokens.Accent : _tokens.LineStrong,
            State == PartRowState.Selected ? _tokens.StrokeSignal : _tokens.StrokeHair,
            _tokens.RadiusMedium,
            glow: State == PartRowState.Selected);

        if (State == PartRowState.Locked)
        {
            _style.BorderWidthLeft = 0;
            _style.BorderWidthTop = 0;
            _style.BorderWidthRight = 0;
            _style.BorderWidthBottom = 0;
        }

        SelfModulate = State is PartRowState.Locked or PartRowState.NoneLeft
            ? new Color(1, 1, 1, _unavailableOpacity)
            : Colors.White;
        QueueRedraw();

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", (int)_tokens.Space2);
        margin.AddThemeConstantOverride("margin_top", (int)VerticalVisibleInset);
        margin.AddThemeConstantOverride("margin_right", (int)_tokens.Space2);
        margin.AddThemeConstantOverride("margin_bottom", (int)VerticalVisibleInset);
        AddChild(margin);

        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.Fill,
        };
        row.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        margin.AddChild(row);

        var iconTint = State == PartRowState.Selected ? _tokens.Accent : _tokens.Ink;
        row.AddChild(UiIcons.Create(PartIconId, UiIconSize.Large, iconTint));

        var label = UiFieldAndRows.Label(PartName, _tokens, _tokens.SmallStrongText, _tokens.Ink);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(label);

        if (State == PartRowState.Locked)
        {
            if (!string.IsNullOrWhiteSpace(CountText))
            {
                row.AddChild(CreateTrailingLabel(CountText));
            }

            row.AddChild(UiFieldAndRows.Icon(UiIconId.Lock, UiIconSize.Standard, _tokens.Ink));
        }
        else
        {
            row.AddChild(CreateTrailingLabel(CountText));
        }
    }

    public override void _Draw()
    {
        base._Draw();
        if (_style is not null)
        {
            DrawStyleBox(_style, VisibleRect);
        }

        if (State != PartRowState.Locked)
        {
            return;
        }

        var stroke = _tokens.StrokeHair;
        var rect = new Rect2(
            VisibleRect.Position + new Vector2(stroke * 0.5f, stroke * 0.5f),
            new Vector2(Math.Max(0, VisibleRect.Size.X - stroke), Math.Max(0, VisibleRect.Size.Y - stroke)));
        UiDashedBorder.DrawRoundedRect(this, rect, Math.Max(0, _tokens.RadiusMedium - (stroke * 0.5f)), _tokens.LineStrong, stroke);
    }

    private bool IsAvailable => State is PartRowState.Rest or PartRowState.Selected;

    private float VerticalVisibleInset =>
        Math.Max(0, (UiComponentContracts.PartRowTouchHeight - UiComponentContracts.PartRowVisibleHeight) * 0.5f);

    private Rect2 VisibleRect =>
        new(Vector2.Down * VerticalVisibleInset, new Vector2(Size.X, Math.Min(Size.Y, UiComponentContracts.PartRowVisibleHeight)));

    private Label CreateTrailingLabel(string text)
    {
        var label = UiFieldAndRows.Label(text, _tokens, _tokens.ReadoutMediumText, _tokens.Ink, HorizontalAlignment.Right);
        label.CustomMinimumSize = new Vector2(_tokens.ColumnSmallWidth, 0);
        label.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        return label;
    }
}
