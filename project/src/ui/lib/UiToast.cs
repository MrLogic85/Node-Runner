using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Short-lived feedback surface with an optional undo action.</summary>
public partial class UiToast : PanelContainer
{
    [Signal]
    public delegate void UndoPressedEventHandler();

    private Label? _messageLabel;
    private Button? _undoButton;
    private Godot.Timer? _dismissTimer;
    private string? _pendingMessage;
    private string? _pendingUndoLabel;
    private float _pendingDuration;
    private bool _refreshingStyle;

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

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged)
        {
            RefreshStyle();
            RefreshContentStyle();
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
        UiThemeLookup.ApplyTypography(_undoButton, UiTokens.Typography.Label);
        _dismissTimer.WaitTime = Mathf.Max(0.1f, durationSeconds);
        _dismissTimer.Start();
        Show();
    }

    private void BuildContent()
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", (int)UiSize.Space.S3);
        margin.AddThemeConstantOverride("margin_top", (int)UiSize.Space.S2);
        margin.AddThemeConstantOverride("margin_right", (int)UiSize.Space.S2);
        margin.AddThemeConstantOverride("margin_bottom", (int)UiSize.Space.S2);
        AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", (int)UiSize.Space.S3);
        margin.AddChild(row);

        _messageLabel = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.AddChild(_messageLabel);

        _undoButton = new Button
        {
            CustomMinimumSize = new Vector2(0, UiSize.Control.Touch),
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
        if (!IsInsideTree() || _refreshingStyle)
        {
            return;
        }

        _refreshingStyle = true;
        try
        {
            var style = UiThemeLookup.CreateFrameStyleBox(this);
            style.BorderColor = UiThemeLookup.Color(this, UiTokens.Color.LineStrong);
            AddThemeStyleboxOverride("panel", style);
        }
        finally
        {
            _refreshingStyle = false;
        }
    }

    private void RefreshContentStyle()
    {
        if (_messageLabel is null || _undoButton is null)
        {
            return;
        }

        UiThemeLookup.ApplyTextStyle(_messageLabel, UiTokens.Typography.Body, UiTokens.Color.Ink);
        UiThemeLookup.ApplyTypography(_undoButton, UiTokens.Typography.Label);
        _undoButton.AddThemeColorOverride("font_color", UiThemeLookup.Color(this, UiTokens.Color.Accent));
        _undoButton.AddThemeColorOverride("font_hover_color", UiThemeLookup.Color(this, UiTokens.Color.Ink));
    }
}
