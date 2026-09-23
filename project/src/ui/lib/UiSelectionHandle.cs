using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>One canvas selection handle, configured as drag, rotate, or scale.</summary>
public partial class UiSelectionHandle : Control
{
    public enum HandleType
    {
        Drag,
        Rotate,
        Scale,
    }

    [Signal]
    public delegate void PressedEventHandler(HandleType type);

    private const float _handleRadius = 13;
    private const float _handleStroke = 2;
    private HandleType _type = HandleType.Drag;
    private UiTokens _tokens = UiTokens.Neon;
    private TextureRect? _icon;

    [Export]
    public HandleType Type
    {
        get => _type;
        set
        {
            _type = value;
            RefreshIcon();
            QueueRedraw();
        }
    }

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            RefreshIcon();
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(44, 44);
        MouseFilter = MouseFilterEnum.Pass;
        RefreshIcon();
        Resized += LayoutIcon;
    }

    public override void _ExitTree()
    {
        Resized -= LayoutIcon;
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (!TryHandlePress(inputEvent, out var position) || position.DistanceTo(HandleCenter()) > _handleRadius)
        {
            return;
        }

        EmitSignal(SignalName.Pressed, (int)Type);
        AcceptEvent();
    }

    public override void _Draw()
    {
        DrawCircle(HandleCenter(), _handleRadius, _tokens.Panel);
        DrawArc(
            HandleCenter(),
            _handleRadius,
            0,
            Mathf.Tau,
            40,
            _tokens.Halo,
            _handleStroke,
            antialiased: true);
    }

    private void RefreshIcon()
    {
        if (!IsInsideTree())
        {
            return;
        }

        if (_icon is not null)
        {
            RemoveChild(_icon);
            _icon.QueueFree();
        }

        _icon = UiIcons.Create(IconFor(Type), UiIconSize.Standard, _tokens.Halo);
        AddChild(_icon);
        LayoutIcon();
    }

    private void LayoutIcon()
    {
        if (_icon is null)
        {
            return;
        }

        var iconSize = _icon.CustomMinimumSize;
        _icon.Position = HandleCenter() - (iconSize * 0.5f);
        _icon.Size = iconSize;
    }

    private static UiIconId IconFor(HandleType type) =>
        type switch
        {
            HandleType.Drag => UiIconId.Move,
            HandleType.Rotate => UiIconId.Rotate,
            HandleType.Scale => UiIconId.Scale,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };

    private static bool TryHandlePress(InputEvent inputEvent, out Vector2 position)
    {
        if (inputEvent is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse)
        {
            position = mouse.Position;
            return true;
        }

        position = Vector2.Zero;
        return false;
    }

    private static Vector2 HandleCenter() => new(22, 22);
}
