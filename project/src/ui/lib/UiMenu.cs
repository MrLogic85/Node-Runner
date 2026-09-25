using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Overlay menu surface that vertically lays out arbitrary child controls.</summary>
[Tool]
[GlobalClass]
public partial class UiMenu : Container
{
    [Signal]
    public delegate void IndexClickedEventHandler(int index);

    public enum MenuWidthMode
    {
        Fixed,
        WrapContent,
    }

    private UiTokens _tokens = UiTokens.Neon;
    private float _width;
    private MenuWidthMode _widthMode;
    private bool _compact;
    private Control? _followAnchor;
    private Vector2 _followPoint = new(0, 1);
    private Vector2 _followOffset;

    [Export]
    public float Width
    {
        get => _width;
        set
        {
            _width = Math.Max(0, value);
            QueueLayout();
        }
    }

    [Export]
    public MenuWidthMode WidthMode
    {
        get => _widthMode;
        set
        {
            _widthMode = value;
            QueueLayout();
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
            ApplyItemSizes();
            QueueLayout();
        }
    }

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _tokens = value;
            QueueLayout();
            QueueRedraw();
        }
    }

    public static float ResolveWidth(MenuWidthMode mode, float width, UiTokens tokens) =>
        mode == MenuWidthMode.WrapContent
            ? 0
            : width > 0 ? width : tokens.MenuWidth;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        ClipChildren = ClipChildrenMode.AndDraw;
        SetProcess(false);
        ApplyItemSizes();
        QueueLayout();
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        Vector2? position = inputEvent switch
        {
            InputEventMouseButton
            {
                ButtonIndex: MouseButton.Left,
                Pressed: true,
            } mouse => mouse.Position,
            InputEventScreenTouch { Pressed: true } touch => touch.Position,
            _ => null,
        };
        if (position is null)
        {
            return;
        }

        var index = ChildIndexAt(position.Value);
        if (index >= 0)
        {
            EmitSignal(SignalName.IndexClicked, index);
        }

        AcceptEvent();
    }

    public override Vector2 _GetMinimumSize()
    {
        var children = VisibleChildren();
        var stroke = Tokens.StrokeHair;
        var contentWidth = children.Length == 0
            ? 0
            : children.Max(child => child.GetCombinedMinimumSize().X);
        var contentHeight = children.Sum(child => child.GetCombinedMinimumSize().Y);
        var fixedWidth = ResolveWidth(WidthMode, Width, Tokens);
        return new Vector2(
            Mathf.Max(fixedWidth, contentWidth + (stroke * 2)),
            contentHeight + (stroke * 2));
    }

    public override void _Notification(int what)
    {
        if (what == NotificationChildOrderChanged)
        {
            ApplyItemSizes();
            QueueLayout();
            return;
        }

        if (what != NotificationSortChildren)
        {
            return;
        }

        var stroke = Tokens.StrokeHair;
        var y = stroke;
        foreach (var child in VisibleChildren())
        {
            var height = child.GetCombinedMinimumSize().Y;
            FitChildInRect(child, new Rect2(
                stroke,
                y,
                Mathf.Max(0, Size.X - (stroke * 2)),
                height));
            y += height;
        }
    }

    public override void _Draw()
    {
        var style = Tokens.FrameStyle(
            UiSurfaceContracts.FrameVariant.Frame,
            UiSurfaceContracts.FrameSize.Flush,
            glow: true);
        style.Draw(GetCanvasItem(), new Rect2(Vector2.Zero, Size));
    }

    /// <summary>
    /// Keeps this top-level menu attached to a normalized point on another control.
    /// The anchor is sampled every frame so scrolling and container relayout are followed.
    /// </summary>
    public void Follow(Control anchor, Vector2 normalizedPoint, Vector2 offset = default)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        _followAnchor = anchor;
        _followPoint = normalizedPoint;
        _followOffset = offset;
        TopLevel = true;
        ZAsRelative = false;
        ZIndex = Math.Max(ZIndex, 100);
        SetProcess(true);
        UpdateFollowPosition();
    }

    public void StopFollowing()
    {
        _followAnchor = null;
        SetProcess(false);
    }

    public override void _Process(double delta) => UpdateFollowPosition();

    private void UpdateFollowPosition()
    {
        if (_followAnchor is null || !IsInstanceValid(_followAnchor) || !IsVisibleInTree())
        {
            return;
        }

        var localPoint = new Vector2(
            _followAnchor.Size.X * _followPoint.X,
            _followAnchor.Size.Y * _followPoint.Y);
        GlobalPosition = _followAnchor.GetGlobalTransformWithCanvas() * localPoint + _followOffset;
    }

    private Control[] VisibleChildren() =>
        GetChildren()
            .OfType<Control>()
            .Where(child => child.Visible && !child.IsSetAsTopLevel())
            .ToArray();

    private int ChildIndexAt(Vector2 position)
    {
        var children = VisibleChildren();
        for (var index = 0; index < children.Length; index++)
        {
            var child = children[index];
            if (child.MouseFilter != MouseFilterEnum.Ignore
                && new Rect2(child.Position, child.Size).HasPoint(position))
            {
                return index;
            }
        }

        return -1;
    }

    private void ApplyItemSizes()
    {
        var size = Compact
            ? UiMenuItem.MenuItemSize.Compact
            : UiMenuItem.MenuItemSize.Standard;
        foreach (var item in GetChildren().OfType<UiMenuItem>())
        {
            item.SizeVariant = size;
        }
    }

    private void QueueLayout()
    {
        UpdateMinimumSize();
        QueueSort();
        QueueRedraw();
    }
}
