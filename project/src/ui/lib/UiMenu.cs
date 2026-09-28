using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Overlay menu surface that vertically lays out arbitrary child controls.</summary>
[Tool]
[GlobalClass]
public partial class UiMenu : Container, IUiClipping
{
    /// <summary>Keeps an open menu above the screen content it follows.</summary>
    private const int _overlayZIndex = 100;

    [Signal]
    public delegate void IndexClickedEventHandler(int index);

    [Signal]
    public delegate void ClosedEventHandler();

    public enum MenuWidthMode
    {
        Fixed,
        WrapContent,
    }

    private float _width;
    private MenuWidthMode _widthMode;
    private bool _compact;
    private Control? _followAnchor;
    private Vector2 _followPoint = new(0, 1);
    private Vector2 _followOffset;
    private int _pressedIndex = -1;
    private bool _swallowingTap;
    private bool _ownsBack;
    private bool _quitOnBack;
    private bool _closingFromBack;

    /// <summary>
    /// For a popup menu: while open, a tap outside it or Back closes it. The tap only closes the
    /// menu and never reaches what is under it.
    /// </summary>
    [Export]
    public bool Dismissible { get; set; }

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

    public static float ResolveWidth(MenuWidthMode mode, float width, float defaultWidth) =>
        mode == MenuWidthMode.WrapContent
            ? 0
            : width > 0 ? width : defaultWidth;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        SetProcess(false);
        ApplyItemSizes();
        QueueLayout();
    }

    public void Close()
    {
        if (!Visible)
        {
            return;
        }

        StopFollowing();
        Hide();
        EmitSignal(SignalName.Closed);
    }

    // Runs before the GUI, so a dismissing tap can be swallowed whole. On a phone each touch
    // arrives twice (touch and emulated mouse), so both kinds are swallowed until release.
    public override void _Input(InputEvent inputEvent)
    {
        if (!Dismissible
            || Engine.IsEditorHint()
            || inputEvent is not (InputEventMouseButton or InputEventScreenTouch or InputEventScreenDrag))
        {
            return;
        }

        if (_swallowingTap)
        {
            GetViewport().SetInputAsHandled();
            if (inputEvent is InputEventMouseButton { Pressed: false } or InputEventScreenTouch { Pressed: false })
            {
                Callable.From(() => _swallowingTap = false).CallDeferred();
            }
            return;
        }

        Vector2? press = inputEvent switch
        {
            InputEventMouseButton { Pressed: true } mouse => mouse.Position,
            InputEventScreenTouch { Pressed: true } touch => touch.Position,
            _ => null,
        };
        if (IsVisibleInTree() && press is { } point && !GetGlobalRect().HasPoint(point))
        {
            Close();
            _swallowingTap = true;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledKeyInput(InputEvent inputEvent)
    {
        if (Dismissible && !Engine.IsEditorHint() && IsVisibleInTree() && inputEvent.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    // Android Back arrives only as a go-back notification, and quits the app unless the tree is
    // told otherwise, so an open dismissible menu takes Back over. After a Back close the previous
    // setting comes back deferred, once the tree has checked it for that Back press; any other
    // close restores it at once, so a screen an item opens can take Back over in turn.
    private void SetOwnsBack(bool owns)
    {
        if (owns == _ownsBack)
        {
            return;
        }

        _ownsBack = owns;
        var tree = GetTree();
        if (owns)
        {
            _quitOnBack = tree.QuitOnGoBack;
            tree.QuitOnGoBack = false;
            return;
        }

        if (!_closingFromBack)
        {
            tree.QuitOnGoBack = _quitOnBack;
            return;
        }

        var quitOnBack = _quitOnBack;
        Callable.From(() => tree.QuitOnGoBack = quitOnBack).CallDeferred();
    }

    // Mouse only: on a phone every touch also arrives as an emulated mouse event, so handling
    // both would click twice. A click lands on release over the row it started on, like a
    // Button, so the row shows its press first and sliding off cancels it.
    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
        {
            return;
        }

        var index = ChildIndexAt(mouse.Position);
        if (mouse.Pressed)
        {
            _pressedIndex = index;
        }
        else
        {
            if (index >= 0 && index == _pressedIndex)
            {
                EmitSignal(SignalName.IndexClicked, index);
            }
            _pressedIndex = -1;
        }

        AcceptEvent();
    }

    public override Vector2 _GetMinimumSize()
    {
        var children = VisibleChildren();
        var stroke = UiSize.Stroke.Hair;
        var contentWidth = children.Length == 0
            ? 0
            : children.Max(child => child.GetCombinedMinimumSize().X);
        var contentHeight = children.Sum(child => child.GetCombinedMinimumSize().Y);
        var fixedWidth = ResolveWidth(
            WidthMode,
            Width,
            UiLayout.MenuWidth);
        return new Vector2(
            Mathf.Max(fixedWidth, contentWidth + (stroke * 2)),
            contentHeight + (stroke * 2));
    }

    void IUiClipping.RefreshClip() => UiClip.Apply(this, clip: true);

    private readonly UiUnsavedState _unsaved = new(CanvasItem.PropertyName.ClipChildren);

    public override void _Notification(int what)
    {
        if (_unsaved.Handle(this, what, () => UiClip.Apply(this, clip: true)))
        {
            return;
        }

        if (what == NotificationEnterTree)
        {
            UiClip.Apply(this, clip: true);
            return;
        }

        if (what == NotificationVisibilityChanged)
        {
            SetOwnsBack(Dismissible && !Engine.IsEditorHint() && IsVisibleInTree());
            return;
        }

        if (what == NotificationExitTree)
        {
            SetOwnsBack(false);
            return;
        }

        if (what == NotificationWMGoBackRequest)
        {
            if (_ownsBack)
            {
                _closingFromBack = true;
                Close();
                _closingFromBack = false;
            }
            return;
        }

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

        var stroke = UiSize.Stroke.Hair;
        var y = (float)stroke;
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
        var style = UiThemeLookup.CreateFrameStyleBox(
            this,
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
        UiClip.Apply(this, clip: true);
        ZAsRelative = false;
        ZIndex = Math.Max(ZIndex, _overlayZIndex);
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

public readonly record struct UiMenuItemSpec(
    string Label,
    UiIconId? Icon = null,
    UiMenuActionItem.MenuItemKind Kind = UiMenuActionItem.MenuItemKind.Default,
    string? Note = null,
    Color? IconTint = null,
    bool Selected = false);

public static class UiMenuItems
{
    public static void Populate(
        UiMenu menu,
        IEnumerable<UiMenuItemSpec> specs,
        bool showSelectedIndicator = false)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(specs);

        foreach (var child in menu.GetChildren())
        {
            menu.RemoveChild(child);
            child.QueueFree();
        }

        foreach (var spec in specs)
        {
            menu.AddChild(new UiMenuActionItem
            {
                LabelText = spec.Label,
                NoteText = spec.Note ?? string.Empty,
                IconId = spec.Icon ?? UiIconId.None,
                Kind = spec.Kind,
                Selected = spec.Selected,
                ShowSelectedIndicator = showSelectedIndicator,
                IconTint = spec.IconTint,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            });
        }
    }
}
