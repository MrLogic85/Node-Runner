using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Hold-to-confirm action with visible progress, cancellation, and completion state.</summary>
public partial class UiHoldButton : Button
{
    [Signal]
    public delegate void HoldCompletedEventHandler();

    private UiTokens _tokens = UiTokens.Neon;
    private UiComponentContracts.HoldState _state;
    private float _holdSeconds;
    private float _progressPercent;

    [Export]
    public string LabelText { get; set; } = "Hold to confirm";

    [Export(PropertyHint.Range, "0,100,1")]
    public float ProgressPercent
    {
        get => _progressPercent;
        set
        {
            _progressPercent = (float)UiComponentContracts.ClampPercent(value);
            QueueRedraw();
        }
    }

    [Export]
    public bool Locked
    {
        get => _state == UiComponentContracts.HoldState.Disabled;
        set
        {
            _state = value ? UiComponentContracts.HoldState.Disabled : UiComponentContracts.HoldState.Rest;
            Refresh();
        }
    }

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            Refresh();
        }
    }

    public UiComponentContracts.HoldState State => _state;

    public override void _Ready()
    {
        ButtonDown += BeginHold;
        ButtonUp += ReleaseHold;
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (_state != UiComponentContracts.HoldState.Holding)
        {
            return;
        }

        _holdSeconds += (float)delta;
        ProgressPercent = _holdSeconds / UiComponentContracts.HoldCompletionSeconds * 100f;
        if (ProgressPercent < 100)
        {
            return;
        }

        _state = UiComponentContracts.HoldState.Completed;
        ProgressPercent = 100;
        Refresh();
        EmitSignal(SignalName.HoldCompleted);
    }

    public override void _Draw()
    {
        base._Draw();
        if (ProgressPercent <= 0)
        {
            return;
        }

        var fillWidth = Size.X * ProgressPercent / 100f;
        DrawRect(new Rect2(Vector2.Zero, new Vector2(fillWidth, Size.Y)), UiTokens.WithAlpha(_tokens.Accent, 0.28f), filled: true);
    }

    private void BeginHold()
    {
        if (Locked || _state == UiComponentContracts.HoldState.Completed)
        {
            return;
        }

        _state = UiComponentContracts.HoldState.Holding;
        _holdSeconds = 0;
        ProgressPercent = 0;
        Refresh();
    }

    private void ReleaseHold()
    {
        if (_state == UiComponentContracts.HoldState.Holding)
        {
            _state = UiComponentContracts.HoldState.Cancelled;
            _holdSeconds = 0;
            ProgressPercent = 0;
            Refresh();
        }
    }

    private void Refresh()
    {
        if (!IsInsideTree())
        {
            return;
        }

        Disabled = Locked;
        Text = _state == UiComponentContracts.HoldState.Completed ? "COMPLETED" : LabelText.ToUpperInvariant();
        CustomMinimumSize = new Vector2(0, _tokens.TouchTarget);
        _tokens.ApplyTextStyle(this, _tokens.LabelText);
        AddThemeColorOverride("font_color", Locked ? _tokens.Muted : _tokens.Danger);
        AddThemeColorOverride("font_hover_color", _tokens.Danger);
        AddThemeStyleboxOverride("normal", CreateStyle(false));
        AddThemeStyleboxOverride("hover", CreateStyle(true));
        AddThemeStyleboxOverride("pressed", CreateStyle(true));
        AddThemeStyleboxOverride("focus", _tokens.FocusRingStyle());
        AddThemeStyleboxOverride("disabled", CreateStyle(false, 0.5f));
        QueueRedraw();
    }

    private StyleBoxFlat CreateStyle(bool active, float opacity = 1) =>
        _tokens.ControlStyle(
            active ? UiTokens.WithAlpha(_tokens.Danger, 0.18f * opacity) : UiTokens.MultiplyAlpha(_tokens.PanelRaised, opacity),
            UiTokens.MultiplyAlpha(_tokens.Danger, opacity),
            active ? _tokens.StrokeSignal : _tokens.StrokeHair,
            glow: false,
            horizontalPadding: UiSpacing.ControlHorizontalPadding(_tokens),
            verticalPadding: UiSpacing.ControlVerticalPadding(_tokens));
}
