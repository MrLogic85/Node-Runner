using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Lets each Android Back press act once. One press sends several go-back signals (see
/// <see cref="UiBackPressTracker"/>); only the first can be taken. A listener calls
/// <see cref="TryTake"/> once it has decided to act, since taking uses up the press. A repeat can
/// neither close a menu and then leave the screen, nor quit the app from the screen the first
/// signal went back to.
/// Registered as the <see cref="AutoloadName"/> autoload; autoloads sit before the scene in the
/// root, so it sees every Back signal and key before any screen does.
/// </summary>
public sealed partial class UiBackPress : Node
{
    public const string AutoloadName = "BackPress";

    private readonly UiBackPressTracker _tracker = new();
    private bool _signalFree;
    private bool _quitHeldBack;

    public override void _EnterTree()
    {
        var root = GetTree().Root;
        root.WindowInput += OnWindowInput;
        root.GoBackRequested += OnGoBackRequested;
    }

    public override void _ExitTree()
    {
        var root = GetTree().Root;
        root.WindowInput -= OnWindowInput;
        root.GoBackRequested -= OnGoBackRequested;
    }

    /// <summary>
    /// True for the one caller that may act on the current Back signal. Call it only once the caller
    /// will act: it uses up the press for every listener after it.
    /// </summary>
    public static bool TryTake(Node from)
    {
        ArgumentNullException.ThrowIfNull(from);
        return from.GetNode<UiBackPress>("/root/" + AutoloadName).Take();
    }

    public override void _Notification(int what)
    {
        if (what != NotificationWMGoBackRequest)
        {
            return;
        }

        _signalFree = _tracker.GoBack(Time.GetTicksMsec(), Input.IsKeyPressed(Key.Back));
        if (!_signalFree && GetTree().QuitOnGoBack)
        {
            GetTree().QuitOnGoBack = false;
            _quitHeldBack = true;
        }
    }

    // The tree reads QuitOnGoBack in its own go_back_requested handler, connected before this one.
    private void OnGoBackRequested()
    {
        if (_quitHeldBack)
        {
            _quitHeldBack = false;
            GetTree().QuitOnGoBack = true;
        }
    }

    private bool Take()
    {
        var free = _signalFree;
        _signalFree = false;
        return free;
    }

    // The root window's input sees every key, even one headed for an open dialog's window.
    private void OnWindowInput(InputEvent input)
    {
        if (input is InputEventKey key && (key.Keycode == Key.Back || key.PhysicalKeycode == Key.Back))
        {
            _tracker.Key(Time.GetTicksMsec(), key.Pressed, key.Echo);
        }
    }
}
