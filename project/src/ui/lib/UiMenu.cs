using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Overlay menu surface that vertically lays out arbitrary child controls.</summary>
[Tool]
[GlobalClass]
public partial class UiMenu : Container, IUiClipping
{
    [Signal]
    public delegate void IndexClickedEventHandler(int index);

    [Signal]
    public delegate void ClosedEventHandler();

    public enum MenuWidthMode
    {
        Fixed,
        WrapContent,
    }

    // As far as a press may move and still be a tap, like UiNotification's.
    private const float _dragSlop = 8;

    private float _width;
    private MenuWidthMode _widthMode;
    private bool _compact;
    private Control? _followAnchor;
    private Vector2 _followPoint = new(0, 1);
    private Vector2 _followOffset;
    private bool _followOpensUpward = true;
    private ScrollContainer? _followScroll;
    private int _pressedIndex = -1;
    private bool _pressing;
    private bool _dragScrolling;
    private Vector2 _pressPosition;
    private float _scrollRemainder;
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
    // Button, so the row shows its press first and sliding off cancels it. The Stop filter keeps
    // the touch itself from reaching what lies beneath, such as Build's canvas (#689).
    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseMotion motion && _pressing)
        {
            DragScroll(motion);
            return;
        }

        if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
        {
            return;
        }

        var index = ChildIndexAt(mouse.Position);
        if (mouse.Pressed)
        {
            _pressedIndex = index;
            _pressing = true;
            _dragScrolling = false;
            _pressPosition = mouse.Position;
            _scrollRemainder = 0;
        }
        else
        {
            if (!_dragScrolling && index >= 0 && index == _pressedIndex)
            {
                EmitSignal(SignalName.IndexClicked, index);
            }
            _pressedIndex = -1;
            _pressing = false;
            _dragScrolling = false;
        }

        AcceptEvent();
    }

    // The overlay keeps a drag on the menu from reaching the panel its anchor scrolls in, so the
    // menu scrolls that panel itself (#1139). Past the tap slop it is a scroll, not a click, and
    // the row's press is let go, as in a ScrollContainer.
    private void DragScroll(InputEventMouseMotion motion)
    {
        if (_followScroll is not { } scroll || !IsInstanceValid(scroll))
        {
            return;
        }

        if (!_dragScrolling)
        {
            if (!IsScrollDrag(_pressPosition, motion.Position))
            {
                return;
            }

            _dragScrolling = true;
            _pressedIndex = -1;
            foreach (var item in GetChildren().OfType<UiMenuItem>())
            {
                item.CancelPress();
            }
        }

        var screenDelta = GetGlobalTransformWithCanvas().BasisXform(motion.Relative);
        var scrollDelta = scroll.GetGlobalTransformWithCanvas().AffineInverse().BasisXform(screenDelta).Y;
        (var step, _scrollRemainder) = ScrollStep(_scrollRemainder, scrollDelta);
        scroll.ScrollVertical -= step;
        AcceptEvent();
    }

    /// <summary>Whether a press that moved from <paramref name="from"/> to <paramref name="to"/> is a scroll, not a tap.</summary>
    public static bool IsScrollDrag(Vector2 from, Vector2 to) => from.DistanceTo(to) > _dragSlop;

    /// <summary>
    /// The whole pixels a drag of <paramref name="delta"/> scrolls, with the fraction carried in
    /// <paramref name="remainder"/>, since a ScrollContainer only takes whole pixels.
    /// </summary>
    public static (int Step, float Remainder) ScrollStep(float remainder, float delta)
    {
        var total = remainder + delta;
        var step = (int)total;
        return (step, total - step);
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
            if (_ownsBack && UiBackPress.TryTake(this))
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

        // Godot only ever grows a free-standing Control to its minimum, so a popup menu whose items
        // were hidden would keep its old height (#876). Inside a Container the parent sizes it; in
        // the editor a resize would be saved into the scene.
        if (!Engine.IsEditorHint() && GetParent() is not Container)
        {
            ResetSize();
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
    /// Keeps this menu attached to a normalized point on another control.
    /// The anchor is sampled every frame so scrolling and container relayout are followed, and a
    /// drag on the menu scrolls the ScrollContainer the anchor sits in (#1139).
    /// A menu that would not fit below the screen edge opens above its anchor when it fits there,
    /// unless <paramref name="opensUpward"/> is false; otherwise it stays below.
    /// The menu must sit in a <see cref="UiLevelLayer"/>, which floats it over its screen (#768).
    /// </summary>
    public void Follow(Control anchor, Vector2 normalizedPoint, Vector2 offset = default, bool opensUpward = true)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        if (GetParent() is not UiLevelLayer)
        {
            throw new InvalidOperationException("Put a following UiMenu in a UiLevelLayer, so it draws over its screen.");
        }
        _followAnchor = anchor;
        _followPoint = normalizedPoint;
        _followOffset = offset;
        _followOpensUpward = opensUpward;
        _followScroll = ScrollContainerOf(anchor);
        UiClip.Apply(this, clip: true);
        SetProcess(true);
        UpdateFollowPosition();
    }

    public void StopFollowing()
    {
        _followAnchor = null;
        _followScroll = null;
        SetProcess(false);
    }

    /// <summary>The nearest ScrollContainer that <paramref name="node"/> scrolls in, if any.</summary>
    public static ScrollContainer? ScrollContainerOf(Node node)
    {
        for (var parent = node.GetParent(); parent is not null; parent = parent.GetParent())
        {
            if (parent is ScrollContainer scroll)
            {
                return scroll;
            }
        }

        return null;
    }

    public override void _Process(double delta) => UpdateFollowPosition();

    private void UpdateFollowPosition()
    {
        if (_followAnchor is null || !IsInstanceValid(_followAnchor) || !IsVisibleInTree())
        {
            return;
        }

        var anchorTransform = _followAnchor.GetGlobalTransformWithCanvas();
        var localPoint = new Vector2(
            _followAnchor.Size.X * _followPoint.X,
            _followAnchor.Size.Y * _followPoint.Y);
        var position = anchorTransform * localPoint + _followOffset;

        position.Y = FollowTop(
            position.Y,
            (anchorTransform * Vector2.Zero).Y,
            _followOffset.Y,
            Size.Y,
            GetViewportRect().End.Y,
            _followOpensUpward);
        GlobalPosition = position;
    }

    /// <summary>
    /// The top of a following menu <paramref name="height"/> tall whose place below its anchor is
    /// <paramref name="below"/>. One that would run past the bottom of the screen opens upward
    /// instead, as Godot's own OptionButton popup does, when it may and fits there.
    /// </summary>
    public static float FollowTop(float below, float anchorTop, float offset, float height, float screenBottom, bool opensUpward)
    {
        if (!opensUpward || below + height <= screenBottom)
        {
            return below;
        }

        var above = anchorTop - offset - height;
        return above >= 0 ? above : below;
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
