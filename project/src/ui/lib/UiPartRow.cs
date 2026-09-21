using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Parts-tray row with glyph, name, count, selected, locked, and zero states.</summary>
public partial class UiPartRow : PanelContainer
{
    [Signal]
    public delegate void PartSelectedEventHandler();

    private UiTokens _tokens = UiTokens.Neon;
    private bool _isZero;
    private UiComponentContracts.SemanticState _state;

    public string Glyph { get; set; } = string.Empty;

    [Export]
    public UiPartIconId PartIconId { get; set; } = UiPartIconId.Servo;

    [Export]
    public string PartName { get; set; } = "Servo";

    [Export]
    public string Count { get; set; } = "1";

    [Export]
    public UiComponentContracts.SemanticState State
    {
        get => _state;
        set
        {
            _state = value;
            Rebuild();
        }
    }

    [Export]
    public bool IsZero
    {
        get => _isZero;
        set
        {
            _isZero = value;
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
        FocusMode = FocusModeEnum.All;
        FocusEntered += Rebuild;
        FocusExited += Rebuild;
        Rebuild();
    }

    public override void _GuiInput(InputEvent @event)
    {
        var activated = @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } ||
                        @event is InputEventScreenTouch { Pressed: true } ||
                        @event is InputEventKey { Pressed: true, Keycode: Key.Enter or Key.Space };
        if (IsAvailable && activated)
        {
            GrabFocus();
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
        var zero = IsZero || State == UiComponentContracts.SemanticState.Disabled || Count == "0";
        var border = State switch
        {
            UiComponentContracts.SemanticState.Selected => _tokens.Accent,
            UiComponentContracts.SemanticState.Locked => _tokens.LineStrong,
            UiComponentContracts.SemanticState.Disabled => _tokens.LineStrong,
            _ => _tokens.LineStrong,
        };
        var focused = HasFocus();
        var style = _tokens.ControlStyle(
            State == UiComponentContracts.SemanticState.Selected ? _tokens.AccentSoft : _tokens.PanelRaised,
            focused ? _tokens.Accent : border,
            focused ? UiTokens.FocusRingStroke : State == UiComponentContracts.SemanticState.Selected ? _tokens.StrokeSignal : _tokens.StrokeHair);
        if (State == UiComponentContracts.SemanticState.Locked)
        {
            style.BorderWidthLeft = 0;
            style.BorderWidthTop = 0;
            style.BorderWidthRight = 0;
            style.BorderWidthBottom = 0;
        }

        AddThemeStyleboxOverride("panel", style);
        QueueRedraw();
        SelfModulate = State == UiComponentContracts.SemanticState.Locked || zero
            ? new Color(1, 1, 1, 0.5f)
            : Colors.White;
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        AddChild(row);
        var glyphTint = State == UiComponentContracts.SemanticState.Selected ? _tokens.Accent : _tokens.Ink;
        row.AddChild(State == UiComponentContracts.SemanticState.Locked
            ? UiIcons.Create(UiIconId.Lock, UiIconSize.Large, _tokens.Muted)
            : UiIcons.Create(PartIconId, UiIconSize.Large, glyphTint));
        var name = UiFieldAndRows.Label(PartName, _tokens, _tokens.BodyStrongText, zero || State == UiComponentContracts.SemanticState.Locked ? _tokens.Muted : _tokens.Ink);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(name);
        row.AddChild(UiFieldAndRows.Label(zero ? "0" : Count, _tokens, _tokens.ReadoutSmallText, zero ? _tokens.Muted : _tokens.Ink, HorizontalAlignment.Right));
    }

    private bool IsAvailable => State != UiComponentContracts.SemanticState.Locked && !IsZero && State != UiComponentContracts.SemanticState.Disabled && Count != "0";

    public override void _Draw()
    {
        base._Draw();
        if (State != UiComponentContracts.SemanticState.Locked)
        {
            return;
        }

        DrawDashedBorder(_tokens.LineStrong);
    }

    private void DrawDashedBorder(Color color)
    {
        const float dash = 4;
        const float gap = 3;
        var inset = _tokens.RadiusMedium * 0.5f;
        DrawDashedLine(new Vector2(inset, 0), new Vector2(Size.X - inset, 0), color, dash, gap);
        DrawDashedLine(new Vector2(inset, Size.Y), new Vector2(Size.X - inset, Size.Y), color, dash, gap);
        DrawDashedLine(new Vector2(0, inset), new Vector2(0, Size.Y - inset), color, dash, gap);
        DrawDashedLine(new Vector2(Size.X, inset), new Vector2(Size.X, Size.Y - inset), color, dash, gap);
    }

    private void DrawDashedLine(Vector2 from, Vector2 to, Color color, float dashLength, float gapLength)
    {
        var direction = to - from;
        var length = direction.Length();
        if (length <= 0)
        {
            return;
        }

        direction /= length;
        for (var distance = 0f; distance < length; distance += dashLength + gapLength)
        {
            DrawLine(
                from + (direction * distance),
                from + (direction * Math.Min(distance + dashLength, length)),
                color,
                _tokens.StrokeHair,
                antialiased: true);
        }
    }
}
