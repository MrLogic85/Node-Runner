using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Canonical row for build parts: icon, name, count, and four reference states.</summary>
public partial class UiPartRow : PanelContainer
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

        CustomMinimumSize = new Vector2(0, _tokens.TouchTarget);
        var style = _tokens.ControlStyle(
            State == PartRowState.Selected ? _tokens.AccentSoft : _tokens.PanelRaised,
            State == PartRowState.Selected ? _tokens.Accent : _tokens.LineStrong,
            State == PartRowState.Selected ? _tokens.StrokeSignal : _tokens.StrokeHair,
            _tokens.RadiusMedium,
            glow: State == PartRowState.Selected,
            horizontalPadding: _tokens.Space2);

        if (State == PartRowState.Locked)
        {
            style.BorderWidthLeft = 0;
            style.BorderWidthTop = 0;
            style.BorderWidthRight = 0;
            style.BorderWidthBottom = 0;
        }

        AddThemeStyleboxOverride("panel", style);
        SelfModulate = State is PartRowState.Locked or PartRowState.NoneLeft
            ? new Color(1, 1, 1, _unavailableOpacity)
            : Colors.White;
        QueueRedraw();

        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.Fill,
        };
        row.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        AddChild(row);

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
        if (State != PartRowState.Locked)
        {
            return;
        }

        var stroke = _tokens.StrokeHair;
        var rect = new Rect2(
            new Vector2(stroke * 0.5f, stroke * 0.5f),
            new Vector2(Math.Max(0, Size.X - stroke), Math.Max(0, Size.Y - stroke)));
        UiDashedBorder.DrawRoundedRect(this, rect, Math.Max(0, _tokens.RadiusMedium - (stroke * 0.5f)), _tokens.LineStrong, stroke);
    }

    private bool IsAvailable => State is PartRowState.Rest or PartRowState.Selected;

    private Label CreateTrailingLabel(string text)
    {
        var label = UiFieldAndRows.Label(text, _tokens, _tokens.ReadoutMediumText, _tokens.Ink, HorizontalAlignment.Right);
        label.CustomMinimumSize = new Vector2(_tokens.ColumnSmallWidth, 0);
        label.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        return label;
    }
}
