using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Screens;

/// <summary>Interactive specimens of the real popup components; callbacks mutate no product data.</summary>
public partial class PopupGalleryScreen : Control
{
    [Signal]
    public delegate void CloseRequestedEventHandler();

    private UiTokens _tokens = UiTokens.Neon;
    private Control _page = null!;
    private Label _status = null!;
    private UiDialog _dialog = null!;
    private UiNotification _notifications = null!;
    private bool _quitOnBack;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        FitViewport();
        GetViewport().SizeChanged += FitViewport;
        MouseFilter = MouseFilterEnum.Stop;
        _quitOnBack = GetTree().QuitOnGoBack;
        GetTree().QuitOnGoBack = false;
        BuildPage();
        _notifications = new UiNotification { Tokens = _tokens };
        AddChild(_notifications);
        _dialog = new UiDialog { Tokens = _tokens };
        AddChild(_dialog);
        _dialog.Finished += OnDialogFinished;
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= FitViewport;
        _dialog.Finished -= OnDialogFinished;
        GetTree().QuitOnGoBack = _quitOnBack;
    }

    private void BuildPage()
    {
        _page = new Control();
        _page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_page);
        var background = new ColorRect { Color = _tokens.Background, MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _page.AddChild(background);
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        UiLayout.ApplyMargins(margin, _tokens);
        _page.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        margin.AddChild(column);
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        column.AddChild(header);
        var title = Text("Popup Gallery", _tokens.HeadingText);
        title.AutowrapMode = TextServer.AutowrapMode.Off;
        header.AddChild(title);
        header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var themes = new UiSegmentedSwitch
        {
            Tokens = _tokens,
            Options = ["Neon", "Paper", "Effects lite"],
            SelectedIndex = _tokens == UiTokens.Paper ? 1 : _tokens.EffectsEnabled ? 0 : 2,
        };
        themes.SelectionChanged += ChangeTheme;
        header.AddChild(themes);
        header.AddChild(Action("Back", Back));
        column.AddChild(Text("DESIGN PROPOSAL / no real data changes", _tokens.OverlineText, _tokens.Halo));
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        column.AddChild(scroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        scroll.AddChild(body);
        body.AddChild(Text("Dialogs / type and hold are independent", _tokens.SubheadingText));
        var dialogs = Flow();
        body.AddChild(dialogs);
        foreach (var type in Enum.GetValues<UiPopupType>())
        {
            var current = type;
            dialogs.AddChild(Action(type.ToString(), () => ShowDialog(new(
                current, $"{current} dialog", "Review this example. Nothing in your creation will change.", "Continue", Succeed))));
        }
        dialogs.AddChild(Action("Hold to delete", () => ShowDialog(new(
            UiPopupType.Danger, "Delete creation?", "\"Walker\" and its 142 generations of training would be removed permanently. Make a copy first if you want to keep it.", "Hold to delete", Succeed, true))));
        dialogs.AddChild(Action("Warning + hold", () => ShowDialog(new(
            UiPopupType.Warn, "Unlock creation?", "This example would reset 142 generations of training while keeping the body.", "Hold to unlock", Succeed, true))));
        dialogs.AddChild(Action("Long content", () => ShowDialog(new(
            UiPopupType.Default, "Review the details",
            string.Join("\n\n", Enumerable.Repeat("The content scrolls while the title and actions remain visible. Cancel, Escape or Android Back safely dismisses the dialog.", 6)), "Continue", Succeed))));
        dialogs.AddChild(Action("Action error", () =>
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
        }));
        dialogs.AddChild(Action("Async action", () => ShowDialog(new(
            UiPopupType.Default, "Wait for the action", "The callback takes two seconds. Closing and repeated activation are blocked until it finishes.",
            "Run action", async () => { await Task.Delay(2000); return UiDialogResult.Success; }))));
        dialogs.AddChild(Action("Close only", () => ShowDialog(new(
            UiPopupType.Default, "Information", "No action button. Close fills the entire action row.",
            AbortText: "Close"))));
        dialogs.AddChild(Action("Default action", () => ShowDialog(new(
            UiPopupType.Default, "Continue?", "No callback is supplied. OK confirms and closes the dialog.",
            ActionText: "OK", AbortText: "Not now"))));
        body.AddChild(Text("Notifications / 5 seconds, click or swipe", _tokens.SubheadingText));
        var notifications = Flow();
        body.AddChild(notifications);
        foreach (var type in Enum.GetValues<UiPopupType>())
        {
            var current = type;
            notifications.AddChild(Action(type.ToString(), () => Enqueue(new(
                current, $"{current} notification", "No click action. Swipe sideways to dismiss."))));
        }
        notifications.AddChild(Action("Click: true", () => Enqueue(new(
            UiPopupType.Default, "New part unlocked: Spring", "Reached 10 m. Tap to simulate opening Achievements.",
            () => { SetStatus("Achievements action ran; returned true."); return true; }))));
        notifications.AddChild(Action("Click: false", () => Enqueue(new(
            UiPopupType.Warn, "Keep this notification", "Tap runs the action but does not dismiss it.",
            () => { SetStatus("Action ran; returned false. Expiry is unchanged."); return false; }))));
        notifications.AddChild(Action("Queue three", () =>
        {
            foreach (var type in Enum.GetValues<UiPopupType>())
            {
                Enqueue(new(type, $"Queued: {type}", "One at a time. Swipe to advance."));
            }
        }));
        _status = Text("Ready. Dialogs block input; notifications do not.", _tokens.NoteText, _tokens.Muted);
        body.AddChild(_status);
        UiNativeScroll.AllowGesturesToBubble(body);
    }

    private static Task<UiDialogResult> Succeed() => Task.FromResult(UiDialogResult.Success);

    private void ShowDialog(UiDialogSpec spec)
    {
        _dialog.Open(spec);
        _notifications.Paused = true;
    }

    private void OnDialogFinished(bool confirmed)
    {
        _notifications.Paused = false;
        SetStatus(confirmed ? "Action succeeded (demo only)." : "Dialog cancelled.");
    }

    private void Enqueue(UiNotificationSpec spec)
    {
        _notifications.Enqueue(spec);
        SetStatus($"Notification queued: {spec.Type}");
    }

    public override void _Input(InputEvent input)
    {
        if (!_dialog.IsOpen && input is InputEventKey && input.IsActionPressed("ui_cancel"))
        {
            Back();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMGoBackRequest && _dialog is not null && !_dialog.IsOpen)
        {
            // Defer navigation so the same Back cannot close a modal and its owning screen.
            Callable.From(Back).CallDeferred();
        }
    }

    private void Back()
    {
        if (_dialog.IsOpen)
        {
            _dialog.TryCancel();
        }
        else if (GetTree().CurrentScene == this)
        {
            GetTree().Quit();
        }
        else
        {
            EmitSignal(SignalName.CloseRequested);
        }
    }

    private void ChangeTheme(int index)
    {
        if (_dialog.IsOpen)
        {
            return;
        }
        _notifications.Clear();
        _tokens = index switch { 1 => UiTokens.Paper, 2 => UiTokens.Neon.WithEffects(false), _ => UiTokens.Neon };
        _dialog.Tokens = _tokens;
        _notifications.Tokens = _tokens;
        _page.Hide();
        _page.QueueFree();
        BuildPage();
        MoveChild(_page, 0);
    }

    private Label Text(string text, UiTokens.TextStyle style, Color? color = null) =>
        UiPopupStyle.Text(text, style, _tokens, color);

    private UiButton Action(string text, Action action)
    {
        var button = new UiButton { Tokens = _tokens, LabelText = text, Compact = true };
        button.Activated += () => action();
        return button;
    }

    private HFlowContainer Flow()
    {
        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", (int)_tokens.Space2);
        flow.AddThemeConstantOverride("v_separation", (int)_tokens.Space2);
        return flow;
    }

    private void FitViewport()
    {
        // Main is a Node2D, so anchors have no parent Control rectangle to fill.
        if (GetParentControl() is null)
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
            Size = GetViewportRect().Size;
        }
    }

    private void SetStatus(string text) => _status.Text = text;
}
