using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Short-lived feedback surface with an optional undo action.</summary>
public partial class UiToast : PanelContainer
{
    [Signal]
    public delegate void UndoPressedEventHandler();

    private UiTokens _tokens = UiTokens.Neon;
    private Label? _messageLabel;
    private Button? _undoButton;
    private Godot.Timer? _dismissTimer;
    private string? _pendingMessage;
    private string? _pendingUndoLabel;
    private float _pendingDuration;

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            RefreshStyle();
            RefreshContentStyle();
        }
    }

    public override void _Ready()
    {
        BuildContent();
        RefreshStyle();
        RefreshContentStyle();
        Hide();
        if (_pendingMessage is not null)
        {
            var message = _pendingMessage;
            var undoLabel = _pendingUndoLabel;
            var duration = _pendingDuration;
            _pendingMessage = null;
            ShowMessage(message, undoLabel, duration);
        }
    }

    public void ShowMessage(string message, string? undoLabel = null, float durationSeconds = 4)
    {
        if (!IsInsideTree() || _messageLabel is null || _undoButton is null || _dismissTimer is null)
        {
            _pendingMessage = message;
            _pendingUndoLabel = undoLabel;
            _pendingDuration = durationSeconds;
            return;
        }

        _messageLabel.Text = message;
        _undoButton.Visible = !string.IsNullOrWhiteSpace(undoLabel);
        _undoButton.Text = undoLabel ?? "Undo";
        _dismissTimer.WaitTime = Mathf.Max(0.1f, durationSeconds);
        _dismissTimer.Start();
        Show();
    }

    private void BuildContent()
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", (int)_tokens.Space3);
        margin.AddThemeConstantOverride("margin_top", (int)_tokens.Space2);
        margin.AddThemeConstantOverride("margin_right", (int)_tokens.Space2);
        margin.AddThemeConstantOverride("margin_bottom", (int)_tokens.Space2);
        AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", (int)_tokens.Space3);
        margin.AddChild(row);

        _messageLabel = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.AddChild(_messageLabel);

        _undoButton = new Button
        {
            CustomMinimumSize = new Vector2(0, _tokens.TouchTarget),
        };
        _undoButton.Pressed += () =>
        {
            _dismissTimer?.Stop();
            Hide();
            EmitSignal(SignalName.UndoPressed);
        };
        row.AddChild(_undoButton);

        _dismissTimer = new Godot.Timer
        {
            OneShot = true,
        };
        _dismissTimer.Timeout += Hide;
        AddChild(_dismissTimer);
    }

    private void RefreshStyle()
    {
        if (!IsInsideTree())
        {
            return;
        }

        AddThemeStyleboxOverride("panel", _tokens.PanelStyle(raised: true, borderColor: _tokens.LineStrong));
    }

    private void RefreshContentStyle()
    {
        if (_messageLabel is null || _undoButton is null)
        {
            return;
        }

        _tokens.ApplyTextStyle(_messageLabel, _tokens.BodyText);
        _messageLabel.AddThemeColorOverride("font_color", _tokens.Ink);
        _tokens.ApplyTextStyle(_undoButton, _tokens.LabelText);
        _undoButton.AddThemeColorOverride("font_color", _tokens.Accent);
        _undoButton.AddThemeColorOverride("font_hover_color", _tokens.Ink);
    }
}
