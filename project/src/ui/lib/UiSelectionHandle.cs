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

    public override void _Draw() => DrawRoundButton(this, HandleCenter(), accentFill: Type == HandleType.Drag);

    /// <summary>
    /// The round button (<c>c_round_button</c>) behind a handle's icon, also drawn by
    /// <see cref="UiInfoRow"/>: a <c>halo</c> ring filled <c>panel</c>, or <c>accent-soft</c> over
    /// <c>bg</c> for the Move handle. Drawn in window pixels (<see cref="UiPixelPen"/>).
    /// </summary>
    internal static void DrawRoundButton(Control control, Vector2 center, bool accentFill)
    {
        using var pen = UiPixelPen.Begin(control);
        var radius = UiSize.Widget.SelectionHandleRadius;
        if (accentFill)
        {
            pen.Disc(center, radius, UiThemeLookup.Color(control, UiTokens.Color.Background));
            pen.Disc(center, radius, UiThemeLookup.Color(control, UiTokens.Color.Accent).WithAlpha(UiThemeLookup.Alpha(control, UiTokens.Alpha.Soft)));
        }
        else
        {
            pen.Disc(center, radius, UiThemeLookup.Color(control, UiTokens.Color.Panel));
        }

        pen.Ring(center, radius, UiThemeLookup.Color(control, UiTokens.Color.Halo), UiSize.Stroke.SelectionHandle, _ringPoints);
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
