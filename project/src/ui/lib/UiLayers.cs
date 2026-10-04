namespace NodeRunner.Ui.Lib;

/// <summary>
/// The named UI levels inside one Viewport or Window, as CanvasLayer numbers (#768). A higher
/// level draws over every lower one in the same Viewport, so the UI never raises a part with
/// ZIndex. A dialog is an embedded Window: it draws over every level of the Viewport it is
/// embedded in, notifications included, and a menu it opens uses these levels inside it.
/// </summary>
public static class UiLayers
{
    /// <summary>Screens and dialog content: the Viewport's own canvas.</summary>
    public const int Screen = 0;

    /// <summary>Menus that float over the screen or dialog that opened them (<see cref="UiLevelLayer"/>).</summary>
    public const int Overlay = Screen + 1;

    /// <summary>The app's notifications (<see cref="UiNotificationLayer"/>), over screens and menus.</summary>
    public const int Notification = Overlay + 1;
}
