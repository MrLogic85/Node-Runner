using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Screens;

/// <summary>Interactive specimens of the real popup components; callbacks mutate no product data.</summary>
public partial class PopupGalleryScreen : GalleryScreen
{
    private UiLabel _status = null!;
    private UiDialog? _dialog;
    private UiNotification? _notifications;

    protected override GalleryPage Page => GalleryPage.PopupGallery;

    protected override bool HasOpenPopup => _dialog is { IsOpen: true };

    public override void _Ready()
    {
        base._Ready();
        MouseFilter = MouseFilterEnum.Stop;
        _status = GetNode<UiLabel>("%Status");
        UiNativeScroll.AllowGesturesToBubble(GetNode<Control>("%ContentFrame"));
        _notifications = new UiNotification();
        AddChild(_notifications);
        _dialog = new UiDialog();
        AddChild(_dialog);
        _dialog.Finished += OnDialogFinished;
        _status.TextColor = UiTokens.Color.Muted;
        GetNode<UiLabel>("%Disclaimer").TextColor = UiTokens.Color.Halo;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        _dialog?.Finished -= OnDialogFinished;
    }

    private void ShowDefaultDialog() => ShowTypeDialog(UiPopupType.Default);
    private void ShowWarningDialog() => ShowTypeDialog(UiPopupType.Warn);
    private void ShowDangerDialog() => ShowTypeDialog(UiPopupType.Danger);

    private void ShowTypeDialog(UiPopupType type) => ShowDialog(new(
        type, $"{type} dialog", "Review this example. Nothing in your creation will change.", "Continue", Succeed));

    private void ShowDeleteDialog() => ShowDialog(new(
        UiPopupType.Danger, "Delete creation?", "\"Walker\" and its 142 generations of training would be removed permanently. Make a copy first if you want to keep it.",
        "Hold to delete", Succeed, true));

    private void ShowUnlockDialog() => ShowDialog(new(
        UiPopupType.Warn, "Unlock creation?", "This example would reset 142 generations of training while keeping the body.",
        "Hold to unlock", Succeed, true));

    private void ShowLongDialog() => ShowDialog(new(
        UiPopupType.Default, "Review the details",
        string.Join("\n\n", Enumerable.Repeat("The content scrolls while the title and actions remain visible. Cancel, Escape or Android Back safely dismisses the dialog.", 6)),
        "Continue", Succeed));

    private void ShowErrorDialog()
    {
        var attempts = 0;
        ShowDialog(new(UiPopupType.Danger, "Try an action",
            "Both buttons are disabled while the callback runs. The first attempt fails; retry succeeds.",
            "Hold to try", async () =>
            {
                await Task.Delay(1500);
                return ++attempts == 1
                    ? UiDialogResult.Failure("The example action failed. Nothing changed; retry or cancel.")
                    : UiDialogResult.Success;
            }, true));
    }

    private void ShowAsyncDialog() => ShowDialog(new(
        UiPopupType.Default, "Wait for the action", "The callback takes two seconds. Closing and repeated activation are blocked until it finishes.",
        "Run action", async () => { await Task.Delay(2000); return UiDialogResult.Success; }));

    private void ShowCloseDialog() => ShowDialog(new(
        UiPopupType.Default, "Information", "No action button. Close fills the entire action row.", AbortText: "Close"));

    private void ShowOptionalActionDialog() => ShowDialog(new(
        UiPopupType.Default, "Continue?", "No callback is supplied. OK confirms and closes the dialog.",
        ActionText: "OK", AbortText: "Not now"));

    private void ShowDefaultNotification() => ShowTypeNotification(UiPopupType.Default);
    private void ShowWarningNotification() => ShowTypeNotification(UiPopupType.Warn);
    private void ShowDangerNotification() => ShowTypeNotification(UiPopupType.Danger);

    private void ShowTypeNotification(UiPopupType type) => Enqueue(new(
        type, $"{type} notification", "No click action. Swipe sideways to dismiss."));

    private void ShowDismissingNotification() => Enqueue(new(
        UiPopupType.Default, "New part unlocked: Spring", "Reached 10 m. Tap to simulate opening Achievements.",
        () => { SetStatus("Achievements action ran; returned true."); return true; },
        Icon: new(UiIconId.PartSpring)));

    private void ShowPersistentNotification() => Enqueue(new(
        UiPopupType.Warn, "Keep this notification", "Tap runs the action but does not dismiss it.",
        () => { SetStatus("Action ran; returned false. Expiry is unchanged."); return false; }));

    private void QueueNotifications()
    {
        foreach (var type in Enum.GetValues<UiPopupType>())
            Enqueue(new(type, $"Queued: {type}", "One at a time. Swipe to advance."));
    }

    private static Task<UiDialogResult> Succeed() => Task.FromResult(UiDialogResult.Success);

    private void ShowDialog(UiDialogSpec spec)
    {
        _dialog!.Open(spec);
    }

    private void OnDialogFinished(bool confirmed)
    {
        SetStatus(confirmed ? "Action succeeded (demo only)." : "Dialog cancelled.");
    }

    private void Enqueue(UiNotificationSpec spec)
    {
        _notifications!.Enqueue(spec);
        SetStatus($"Notification queued: {spec.Type}");
    }

    // The theme switch is in the toolbar; queued notifications belong to the old theme.
    protected override void OnThemeApplied() => _notifications?.Clear();

    private void SetStatus(string text) => _status.Text = text;
}
