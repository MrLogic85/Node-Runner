using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Reference toolbar (<c>ComponentToolbars</c>): Back, one flexible field for the
/// screen's title and trailing actions, then the overflow, always last.
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

    public override void _Notification(int what) => _unsaved.Handle(this, what);

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
