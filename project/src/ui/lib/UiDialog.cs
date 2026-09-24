using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Modal Window using the shared card/buttons and an asynchronous action result.</summary>
public sealed partial class UiDialog : Window
{
    [Signal]
    public delegate void FinishedEventHandler(bool confirmed);

    public UiTokens Tokens { get; set; } = UiTokens.Neon;
    public bool IsOpen { get; private set; }
    public bool IsBusy { get; private set; }
    public string? ErrorMessage { get; private set; }

    private UiDialogSpec? _spec;
    private UiDialogContent _content = null!;
    private Viewport? _host;
    private bool _quitOnBack;
    private int _operation;

    public UiDialog()
    {
        Visible = false;
        Transient = true;
        Exclusive = true;
        Borderless = true;
        Unresizable = true;
        Transparent = true;
        TransparentBg = true;
        ForceNative = false;
    }

    public override void _Ready()
    {
        _content = GD.Load<PackedScene>("res://scenes/ui/UiDialogContent.tscn").Instantiate<UiDialogContent>();
        AddChild(_content);
        _content.AbortButton.Activated += OnCloseRequested;
        _content.ActionButton.Activated += OnConfirm;
        _host = GetParent().GetViewport();
        _host.SizeChanged += FitHost;
        CloseRequested += OnCloseRequested;
        WindowInput += OnWindowInput;
        SizeChanged += QueueLayout;
    }

    /// <summary>Add to the scene tree before opening. Tokens are applied when opening.</summary>
    public void Open(UiDialogSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(spec.Action);
        ArgumentException.ThrowIfNullOrWhiteSpace(spec.AbortText);
        if (!IsNodeReady() || !IsInsideTree())
        {
            throw new InvalidOperationException("Add UiDialog to the scene tree before opening it.");
        }
        if (IsOpen)
        {
            throw new InvalidOperationException("UiDialog is already open.");
        }
        if (!_host!.GuiEmbedSubwindows)
        {
            throw new InvalidOperationException("UiDialog requires an embedding viewport (GuiEmbedSubwindows).");
        }
        _spec = spec;
        ErrorMessage = null;
        IsBusy = false;
        _operation++;
        _content.Bind(spec, Tokens);
        Title = spec.Title;
        _quitOnBack = GetTree().QuitOnGoBack;
        GetTree().QuitOnGoBack = false;
        IsOpen = true;
        FitHost();
        Popup();
        _content.AbortButton.GrabFocus();
        QueueLayout();
    }

    public bool TryCancel()
    {
        if (!IsOpen || IsBusy)
        {
            return false;
        }
        Finish(false);
        return true;
    }

    /// <summary>Runs at most one action at a time, including synchronous callbacks.</summary>
    public async Task ConfirmAsync()
    {
        if (!IsOpen || IsBusy || _spec is null)
        {
            return;
        }
        if (!_spec.HasAction)
        {
            GD.PushWarning("Cannot confirm a dialog without an action button.");
            return;
        }
        var operation = _operation;
        var spec = _spec;
        IsBusy = true;
        ErrorMessage = null;
        _content.ShowError(null);
        _content.SetBusy(true);
        QueueLayout();

        UiDialogResult result;
        try
        {
            result = await spec.Action()
                ?? throw new InvalidOperationException("Dialog action returned no result.");
        }
        catch (Exception exception)
        {
            // User callbacks form an async UI boundary: log details, show a safe retry message.
            GD.PushError($"Dialog action failed: {exception}");
            result = UiDialogResult.Failure("The action could not be completed. Please try again.");
        }

        // The owner may have removed the screen while its operation was in flight.
        if (operation != _operation || !GodotObject.IsInstanceValid(this) || !IsInsideTree() || !IsOpen)
        {
            return;
        }
        IsBusy = false;
        if (result.Succeeded)
        {
            Finish(true);
            return;
        }
        ErrorMessage = result.ErrorMessage;
        _content.ShowError(ErrorMessage);
        _content.SetBusy(false);
        _content.ActionButton.HoldToActivate = spec.HoldToAction;
        _content.AbortButton.GrabFocus();
        QueueLayout();
    }

    private void Finish(bool confirmed)
    {
        IsOpen = false;
        _operation++;
        _spec = null;
        GetTree().QuitOnGoBack = _quitOnBack;
        Hide();
        EmitSignal(SignalName.Finished, confirmed);
    }

    private void OnConfirm() => _ = ConfirmAsync();
    private void OnCloseRequested() => TryCancel();

    private void OnWindowInput(InputEvent input)
    {
        if (!input.IsEcho() && (input.IsActionPressed("ui_cancel") || input.IsActionPressed("ui_close_dialog")))
        {
            SetInputAsHandled();
            TryCancel();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMGoBackRequest)
        {
            TryCancel();
        }
    }

    public override void _ExitTree()
    {
        _operation++;
        if (IsOpen)
        {
            GetTree().QuitOnGoBack = _quitOnBack;
        }
        IsOpen = false;
        IsBusy = false;
        _spec = null;
        if (_host is not null)
        {
            _host.SizeChanged -= FitHost;
        }
        CloseRequested -= OnCloseRequested;
        WindowInput -= OnWindowInput;
        SizeChanged -= QueueLayout;
        _content.AbortButton.Activated -= OnCloseRequested;
        _content.ActionButton.Activated -= OnConfirm;
    }

    private void FitHost()
    {
        if (!IsOpen)
        {
            return;
        }
        Position = Vector2I.Zero;
        Size = (Vector2I)_host!.GetVisibleRect().Size;
        LayoutCard();
    }

    private void QueueLayout() => Callable.From(LayoutCard).CallDeferred();

    private void LayoutCard()
    {
        if (!IsOpen)
        {
            return;
        }
        _content.LayoutCard();
    }
}
