using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Compatibility facade for the destructive hold variant of the canonical button.</summary>
public partial class UiHoldButton : UiTertiaryButton
{
    [Signal]
    public delegate void HoldCompletedEventHandler();

    private UiComponentContracts.HoldState _state;

    public UiHoldButton()
    {
        HoldDurationSeconds = UiComponentContracts.HoldCompletionSeconds;
    }

    [Export]
    public float ProgressPercent
    {
        get => Progress < 0 ? 0 : Progress * 100;
        set => Progress = (float)UiComponentContracts.ClampPercent(value) / 100;
    }

    [Export]
    public bool Locked
    {
        get => !Enabled;
        set
        {
            Enabled = !value;
            _state = value
                ? UiComponentContracts.HoldState.Disabled
                : UiComponentContracts.HoldState.Rest;
            Progress = 0;
            RefreshStyle();
        }
    }

    public UiComponentContracts.HoldState State => _state;

    protected override bool CanBeginHold =>
        UiComponentContracts.CanBeginHold(_state);

    protected override void OnHoldStarted()
    {
        _state = UiComponentContracts.HoldState.Holding;
    }

    protected override void OnHoldCancelled()
    {
        _state = UiComponentContracts.HoldState.Cancelled;
    }

    protected override void OnHoldCompleted()
    {
        _state = UiComponentContracts.HoldState.Completed;
        RefreshStyle();
        EmitSignal(SignalName.HoldCompleted);
    }

    protected override string DisplayText =>
        State == UiComponentContracts.HoldState.Completed
            ? "COMPLETED"
            : base.DisplayText;
}
