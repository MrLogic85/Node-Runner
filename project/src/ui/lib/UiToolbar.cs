using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Reference toolbar (<c>ComponentToolbars</c>): Back, one flexible field for the
/// screen's title and trailing actions, then the overflow, always last.
/// The field scrolls sideways and has no minimum width, so content that does not
/// fit is clipped and never pushes Back or the overflow off screen (#737); it
/// clips only then, so the glow of actions that fit is not cut.
/// The toolbar owns the overflow menu's node, its position under the overflow
/// button, opening it, and closing it on a tap outside or Back; the screen fills
/// <see cref="Menu"/> and decides what each item does.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiToolbar : MarginContainer
{
    [Signal]
    public delegate void BackPressedEventHandler();

    private const float _unbounded = -1;
    private static readonly Vector2 _overflowBottomRight = Vector2.One;

    private UiButton? _back;
    private ScrollContainer? _field;
    private Control? _fieldContent;
    private UiButton? _overflow;
    private bool _showBack = true;
    private bool _showOverflow = true;

    /// <summary>Hides Back on a screen that has nowhere to go back to (e.g. run on its own with F6).</summary>
    [Export]
    public bool ShowBack
    {
        get => _showBack;
        set
        {
            _showBack = value;
            _back?.Visible = value;
        }
    }

    /// <summary>Hides the overflow on a screen that has no menu.</summary>
    [Export]
    public bool ShowOverflow
    {
        get => _showOverflow;
        set
        {
            _showOverflow = value;
            _overflow?.Visible = value;
        }
    }

    public UiMenu Menu => GetNode<UiMenu>("%ToolbarMenu");

    private readonly UiUnsavedState _unsaved = new(
        [
            ("%Back", CanvasItem.PropertyName.Visible),
            ("%Overflow", CanvasItem.PropertyName.Visible),
        ]);

    // The height is the scene's own (#331); the bar never grows past it.
    public override Vector2 _GetMaximumSize() => new(_unbounded, CustomMinimumSize.Y);

    public override void _Notification(int what)
    {
        if (!_unsaved.Handle(this, what) && what == NotificationThemeChanged)
        {
            QueueRedraw();
        }
    }

    public override void _Draw() =>
        DrawRect(new Rect2(Vector2.Zero, Size), UiThemeLookup.Color(this, UiTokens.Color.Panel));

    public override void _Ready()
    {
        _back = GetNode<UiButton>("%Back");
        _overflow = GetNode<UiButton>("%Overflow");
        _back.Visible = ShowBack;
        _overflow.Visible = ShowOverflow;
        Menu.Hide();
        if (Engine.IsEditorHint())
        {
            return;
        }

        _back.Activated += OnBackActivated;
        _overflow.Activated += ToggleMenu;
        Menu.Dismissible = true;
        Menu.Resized += FollowOverflow;
        _field = GetNode<ScrollContainer>("%Field");
        _fieldContent = _field.GetChild<Control>(0);
        _field.Resized += ClipOnlyOverflow;
        _fieldContent.MinimumSizeChanged += ClipOnlyOverflow;
        ClipOnlyOverflow();
    }

    public override void _ExitTree()
    {
        if (!Engine.IsEditorHint() && IsInstanceValid(Menu) && Menu.Visible)
        {
            CloseMenu();
        }
    }

    public void CloseMenu() => Menu.Close();

    private void ToggleMenu()
    {
        if (Menu.Visible)
        {
            CloseMenu();
            return;
        }

        Menu.Show();
        FollowOverflow();
    }

    // A ScrollContainer always clips, which would cut the glow of toolbar actions even when they fit.
    private void ClipOnlyOverflow()
    {
        if (_field is null || _fieldContent is null)
        {
            return;
        }

        _field.ClipContents = _fieldContent.GetCombinedMinimumSize().X > _field.Size.X;
    }

    private void OnBackActivated() => EmitSignal(SignalName.BackPressed);

    private void FollowOverflow()
    {
        if (_overflow is null || !Menu.Visible)
        {
            return;
        }

        Menu.Follow(_overflow, _overflowBottomRight, new Vector2(-Menu.Size.X, UiSize.Space.S1));
    }
}
