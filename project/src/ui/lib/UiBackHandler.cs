using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Gives the screen it is added to Android Back and Escape while it is in the tree: instead of
/// quitting, Back raises <see cref="BackRequested"/>. Add it with <c>InternalMode.Front</c> so it
/// hears Back before the menus and popups inside the screen; while <see cref="CanTakeBack"/> is
/// false (a menu or dialog is open) they handle Back themselves.
/// </summary>
public sealed partial class UiBackHandler : Node
{
    private bool _ownsBack;
    private bool _quitOnBackBefore;

    public event Action? BackRequested;

    /// <summary>False while something inside the screen, such as an open menu, handles Back.</summary>
    public Func<bool>? CanTakeBack { get; set; }

    public override void _EnterTree()
    {
        if (Engine.IsEditorHint())
        {
            return;
        }

        _ownsBack = true;
        _quitOnBackBefore = GetTree().QuitOnGoBack;
        GetTree().QuitOnGoBack = false;
    }

    public override void _ExitTree()
    {
        if (_ownsBack)
        {
            _ownsBack = false;
            GetTree().QuitOnGoBack = _quitOnBackBefore;
        }
    }

    // Deferred, so one Back press cannot also reach the screen that opens next. Android repeats the
    // press; UiBackPress lets only the first act.
    public override void _Notification(int what)
    {
        if (what == NotificationWMGoBackRequest && CanTake() && UiBackPress.TryTake(this))
        {
            Callable.From(RequestBack).CallDeferred();
        }
    }

    // A menu deeper in the tree takes Escape first.
    public override void _UnhandledKeyInput(InputEvent inputEvent)
    {
        if (inputEvent.IsActionPressed("ui_cancel") && CanTake())
        {
            GetViewport().SetInputAsHandled();
            RequestBack();
        }
    }

    private bool CanTake() =>
        _ownsBack
        && (GetParent() is not CanvasItem screen || screen.IsVisibleInTree())
        && (CanTakeBack?.Invoke() ?? true);

    private void RequestBack() => BackRequested?.Invoke();
}
