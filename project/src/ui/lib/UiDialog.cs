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
    private UiCard? _card;
    private ScrollContainer? _scroll;
    private Label? _body;
    private Label? _error;
    private UiButton? _cancel;
    private UiButton? _confirm;
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
        BuildContent(spec);
        Title = spec.Title;
        _quitOnBack = GetTree().QuitOnGoBack;
        GetTree().QuitOnGoBack = false;
        IsOpen = true;
        FitHost();
        Popup();
        _cancel!.GrabFocus();
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
        _error!.Hide();
        _cancel!.Enabled = false;
        _confirm!.Enabled = false;
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
        _error.Text = ErrorMessage;
        _error.Show();
        _cancel.Enabled = true;
        _confirm.Enabled = true;
        _confirm.Progress = spec.HoldToAction ? 0 : -1;
        _cancel.GrabFocus();
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

    private void BuildContent(UiDialogSpec spec)
    {
        _confirm = null;
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        var scrim = new ColorRect { Color = Tokens.Scrim, MouseFilter = Control.MouseFilterEnum.Stop };
        scrim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(scrim);
        _card = UiPopupStyle.Card(spec.Type, Tokens);
        _card.MinimumSizeChanged += QueueLayout;
        AddChild(_card);
        _card.MouseFilter = Control.MouseFilterEnum.Stop;
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", (int)Tokens.Space3);
        _card.AddChild(column);
        column.AddChild(UiPopupStyle.Heading(spec.Type, spec.Title, Tokens));
        _scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        column.AddChild(_scroll);
        _body = UiPopupStyle.Text(spec.Content, Tokens.BodyText, Tokens);
        _body.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _scroll.AddChild(_body);
        _error = UiPopupStyle.Text("", Tokens.NoteText, Tokens, Tokens.Danger);
        _error.Name = "Error";
        _error.Visible = false;
        column.AddChild(_error);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", (int)Tokens.Space2);
        column.AddChild(actions);
        _cancel = new UiButton
        {
            Name = "Cancel",
            Tokens = Tokens,
            LabelText = spec.AbortText,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _cancel.Activated += OnCloseRequested;
        _cancel.MinimumSizeChanged += QueueLayout;
        actions.AddChild(_cancel);
        if (!spec.HasAction)
        {
            return;
        }
        _confirm = new UiButton
        {
            Name = "Confirm",
            Tokens = Tokens,
            LabelText = spec.ActionText!,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Kind = spec.Type switch
            {
                UiPopupType.Warn => UiButtonKind.Flat,
                UiPopupType.Danger => UiButtonKind.Tertiary,
                _ => UiButtonKind.Primary,
            },
            HoldDurationSeconds = spec.HoldToAction ? UiComponentContracts.HoldCompletionSeconds : 0,
        };
        _confirm.Activated += OnConfirm;
        _confirm.MinimumSizeChanged += QueueLayout;
        actions.AddChild(_confirm);
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
        if (!IsOpen || _card is null || _scroll is null || _body is null)
        {
            return;
        }
        var available = (Vector2)Size - Vector2.One * Tokens.Space4 * 2;
        var actionWidth = Mathf.Max(_cancel!.GetMinimumSize().X, _confirm?.GetMinimumSize().X ?? 0);
        _cancel.CustomMinimumSize = new Vector2(actionWidth, Tokens.ControlHeight);
        if (_confirm is not null)
        {
            _confirm.CustomMinimumSize = _cancel.CustomMinimumSize;
        }
        var width = Mathf.Min(Tokens.DialogWidth, available.X);
        _card.CustomMinimumSize = new Vector2(width, 0);
        _card.Size = new Vector2(width, 0);
        using var measured = new TextParagraph();
        measured.AddString(_body.Text, _body.GetThemeFont("font"), (int)Tokens.BodyText.FontSize);
        measured.Width = Mathf.Max(1, width - Tokens.Space3 * 2 - Tokens.Space4);
        var fixedHeight = _card.GetCombinedMinimumSize().Y - _scroll.GetCombinedMinimumSize().Y;
        _scroll.CustomMinimumSize = new Vector2(0, Mathf.Min(measured.GetSize().Y, Mathf.Max(Tokens.ControlHeight, available.Y - fixedHeight)));
        _card.Size = new Vector2(width, 0);
        _card.Position = ((Vector2)Size - _card.Size) / 2;
    }
}
