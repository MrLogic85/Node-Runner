using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>One canvas selection handle, configured as drag, rotate, or scale.</summary>
[Tool]
[GlobalClass]
public partial class UiSelectionHandle : Control, ISerializationListener
{
    private const int _ringPoints = 40;

    public enum HandleType
    {
        Drag,
        Rotate,
        Scale,
    }

    [Signal]
    public delegate void PressedEventHandler(HandleType type);

    private HandleType _type = HandleType.Drag;
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

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        RecoverIcon();
        RefreshIcon();
    }

    public override void _EnterTree() => RequestReady();

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize() => CallDeferred(MethodName.RestoreContent);

    private void RestoreContent()
    {
        if (!IsInsideTree())
        {
            return;
        }

        RecoverIcon();
        RefreshIcon();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged && IsNodeReady())
        {
            RefreshIcon();
            QueueRedraw();
        }
        else if (what == NotificationResized)
        {
            LayoutIcon();
        }
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (!TryHandlePress(inputEvent, out var position) || position.DistanceTo(HandleCenter()) > UiSize.Widget.SelectionHandleRadius)
        {
            return;
        }

        EmitSignal(SignalName.Pressed, (int)Type);
        AcceptEvent();
    }

    public override void _Draw()
    {
        var center = HandleCenter();
        const int radius = UiSize.Widget.SelectionHandleRadius;
        DrawCircle(center, radius, UiThemeLookup.Color(this, UiTokens.Color.Panel));
        DrawArc(
            center,
            radius,
            0,
            Mathf.Tau,
            _ringPoints,
            UiThemeLookup.Color(this, UiTokens.Color.Halo),
            UiSize.Stroke.SelectionHandle,
            antialiased: false);
    }

    public override Vector2 _GetMinimumSize() => Vector2.One * UiSize.Widget.SelectionHandleSize;

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

        var tint = UiThemeLookup.Color(this, UiTokens.Color.Halo);
        _icon = UiIcons.Create(IconFor(Type), UiIconSize.Standard, tint);
        AddChild(_icon, false, InternalMode.Front);
        LayoutIcon();
    }

    private void RecoverIcon()
    {
        _icon = GetChildren(includeInternal: true)
            .OfType<TextureRect>()
            .FirstOrDefault();
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

    private static Vector2 HandleCenter() => Vector2.One * (UiSize.Widget.SelectionHandleSize * 0.5f);
}
