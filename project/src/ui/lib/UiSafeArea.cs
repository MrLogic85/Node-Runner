using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Space to keep free at each screen edge, in canvas units.</summary>
public readonly record struct UiInsets(float Left, float Top, float Right, float Bottom)
{
    public static UiInsets None { get; } = new(0, 0, 0, 0);
}

/// <summary>
/// The part of the window that controls can use: Godot's display safe area, which on a phone
/// leaves out the camera cutout (#513). The app hides the phone's status and navigation bars
/// (immersive mode), so the safe area only has to clear the cutout.
/// Registered as the <see cref="AutoloadName"/> autoload, so every screen reads the same insets and
/// hears <see cref="Changed"/> when they move.
/// </summary>
public sealed partial class UiSafeArea : Node
{
    public const string AutoloadName = "SafeArea";

    // Turning a phone 180 degrees moves the cutout to the other side without changing the window's
    // size, and Godot reports no event for it, so a phone also checks the safe area this often.
    private const double _pollSeconds = 0.25;

    [Signal]
    public delegate void ChangedEventHandler();

    /// <summary>Space to keep free at each edge right now, in canvas units.</summary>
    public UiInsets Current { get; private set; } = UiInsets.None;

    /// <summary>The autoload, or null in the editor, where it does not run.</summary>
    public static UiSafeArea? Of(Node from)
    {
        ArgumentNullException.ThrowIfNull(from);
        return Engine.IsEditorHint() ? null : from.GetNode<UiSafeArea>("/root/" + AutoloadName);
    }

    public override void _Ready()
    {
        GetViewport().SizeChanged += Refresh;
        if (OS.HasFeature("mobile"))
        {
            var poll = new Godot.Timer { WaitTime = _pollSeconds, Autostart = true, ProcessMode = ProcessModeEnum.Always };
            poll.Timeout += Refresh;
            AddChild(poll);
        }

        Refresh();
    }

    public override void _ExitTree() => GetViewport().SizeChanged -= Refresh;

    private void Refresh()
    {
        var window = new Rect2I(DisplayServer.WindowGetPosition(), DisplayServer.WindowGetSize());
        var insets = Insets(window, DisplayServer.GetDisplaySafeArea(), GetViewport().GetVisibleRect().Size);
        if (insets == Current)
        {
            return;
        }

        Current = insets;
        EmitSignal(SignalName.Changed);
    }

    /// <summary>
    /// Converts the safe area, in screen pixels, into insets of the window in canvas units. Only the
    /// part of the window outside the safe area counts; an empty safe area means none is reported.
    /// </summary>
    public static UiInsets Insets(Rect2I window, Rect2I safeArea, Vector2 canvasSize)
    {
        if (window.Size.X <= 0 || window.Size.Y <= 0 || safeArea.Size.X <= 0 || safeArea.Size.Y <= 0)
        {
            return UiInsets.None;
        }

        var unitsPerPixel = canvasSize.X / window.Size.X;
        return new UiInsets(
            Mathf.Max(0, safeArea.Position.X - window.Position.X) * unitsPerPixel,
            Mathf.Max(0, safeArea.Position.Y - window.Position.Y) * unitsPerPixel,
            Mathf.Max(0, window.End.X - safeArea.End.X) * unitsPerPixel,
            Mathf.Max(0, window.End.Y - safeArea.End.Y) * unitsPerPixel);
    }
}
