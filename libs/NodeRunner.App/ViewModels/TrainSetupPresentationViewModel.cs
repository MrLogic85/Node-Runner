using NodeRunner.App.Navigation;
using NodeRunner.App.Services;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// One stepped slider row: its label, its readout, its thumb at 0…1 and the distance between two
/// whole steps on that scale, so the thumb stops only where the value does. A disabled row
/// is shown dimmed and cannot be dragged. A null <paramref name="Position"/> shows no thumb, for a
/// value the scale does not hold. A <paramref name="HighPosition"/> makes it a range, from the
/// <paramref name="Position"/> thumb to this one.
/// </summary>
public sealed record SettingSlider(UiText Label, UiText Readout, double? Position, double Step, bool Disabled = false, double? HighPosition = null);

/// <summary>
/// The line under the Shadows slider: a quiet note while <see cref="ShadowsLoad.Smooth"/>, else a
/// warning whose colour rises with the load.
/// </summary>
public sealed record SettingNote(UiText Text, ShadowsLoad Load);

/// <summary>
/// Train setup, between Build and Training: Train or Simulate, the Shadows and Run length
/// sliders and the Run length mode, filled from the Creation's saved values or, before its first
/// Start, the Shadows default from Settings and the default Run length. Training starts by saving
/// <see cref="Settings"/> on the Creation. Simulate needs a trained Creation, so an
/// untrained one has no Train or Simulate choice at all; it plays one shadow until the player
/// leaves, so it dims both sliders and saves nothing. The selected map
/// card names <see cref="Maps.Default"/>. The map choice comes later (#540).
/// </summary>
public sealed class TrainSetupPresentationViewModel
{
    public static SettingRange ShadowsRange { get; } = new(TrainSettingsDef.MinShadows, TrainSettingsDef.MaxShadows, 1);

    public static SettingRange RunLengthRange { get; } = new(TrainSettingsDef.MinRunLengthSeconds, TrainSettingsDef.MaxRunLengthSeconds, TrainSettingsDef.RunLengthStepSeconds);

    /// <param name="creation">The Creation to set up.</param>
    /// <param name="defaultShadows">The Shadows default from Settings, for a Creation without Train setup values.</param>
    public TrainSetupPresentationViewModel(CreationDef creation, int defaultShadows)
    {
        ArgumentNullException.ThrowIfNull(creation);
        CreationId = creation.Id;
        Title = UiText.Format("Train {0}", creation.Name);
        Subtitle = creation.Training is { } training
            ? UiText.Counted("{0} generation so far", "{0} generations so far", training.Generation)
            : UiText.Plain("Not trained yet");
        Settings = creation.TrainSettings ?? new TrainSettingsDef(defaultShadows, TrainSettingsDef.Default.RunLengthSeconds);
        ShadowsBudget = ShadowsBudget.For(creation.Creature);
        CanSimulate = creation.Training is not null;
        HasPoweredParts = NetworkPorts.Of(creation.Creature).Outputs.Count > 0;
    }

    public event EventHandler? Changed;

    public Guid CreationId { get; }

    public UiText Title { get; }

    public UiText Subtitle { get; }

    public UiText MapName => MapNames.Of(Maps.Default.Id);

    public TrainSettingsDef Settings { get; private set; }

    public TrainingRunMode Mode { get; private set; }

    /// <summary>How many shadows this creature can race smoothly, and before slow motion.</summary>
    public ShadowsBudget ShadowsBudget { get; }

    /// <summary>
    /// Whether Simulate can be chosen: only a trained Creation has a network to play. Without it there
    /// is nothing to choose, so Train setup hides the Train or Simulate switch.
    /// </summary>
    public bool CanSimulate { get; }

    /// <summary>
    /// Whether the network has a powered part to drive. Without one the creature may still train, but
    /// it only stands still, so Train setup warns at the top.
    /// </summary>
    public bool HasPoweredParts { get; }

    /// <summary>The one line under Train or Simulate saying what the chosen mode does.</summary>
    public UiText ModeNote => Mode switch
    {
        TrainingRunMode.Simulate => UiText.Plain("Plays the trained network with one shadow. Nothing is learned or saved."),
        _ => UiText.Plain("Shadows race and the network keeps learning."),
    };

    public SettingSlider Shadows => Mode == TrainingRunMode.Simulate
        ? new(UiText.Plain("Shadows"), UiText.Number(1), null, ShadowsRange.PositionStep, Disabled: true)
        : new(
            UiText.Plain("Shadows"),
            UiText.Number(Settings.Shadows),
            ShadowsRange.Position(Settings.Shadows),
            ShadowsRange.PositionStep);

    /// <summary>
    /// The line under the Shadows slider: why more shadows help, or a warning above the smooth
    /// limit. It never blocks Start or changes the value. Simulate has no line.
    /// </summary>
    public SettingNote? ShadowsNote => Mode == TrainingRunMode.Simulate
        ? null
        : ShadowsBudget.LoadOf(Settings.Shadows) switch
        {
            ShadowsLoad.TooMany => new(UiText.Plain("Probably too many shadows."), ShadowsLoad.TooMany),
            ShadowsLoad.Caution => new(UiText.Format("Phone may stutter above {0}.", ShadowsBudget.SmoothLimit), ShadowsLoad.Caution),
            _ => new(UiText.Plain("The network learns faster with more shadows."), ShadowsLoad.Smooth),
        };

    public SettingSlider RunLength => Mode == TrainingRunMode.Simulate
        ? new(UiText.Plain("Run length"), UiText.Plain("Until you leave"), null, RunLengthRange.PositionStep, Disabled: true)
        : Settings is { RunLengthMode: RunLengthMode.Ranged, RunLengthRange: { } range }
            ? new(
                UiText.Plain("Run length"),
                range.LowSeconds == range.HighSeconds
                    ? Seconds(range.LowSeconds)
                    : UiText.Format("{0}–{1} s", range.LowSeconds, range.HighSeconds),
                RunLengthRange.Position(range.LowSeconds),
                RunLengthRange.PositionStep,
                HighPosition: RunLengthRange.Position(range.HighSeconds))
            : new(
                UiText.Plain("Run length"),
                Seconds(Settings.RunLengthSeconds),
                RunLengthRange.Position(Settings.RunLengthSeconds),
                RunLengthRange.PositionStep);

    /// <summary>
    /// Whether the Run length mode shows: Simulate plays until the player leaves, so it has no
    /// mode to choose and Train setup hides it with its note.
    /// </summary>
    public bool ShowsRunLengthMode => Mode != TrainingRunMode.Simulate;

    /// <summary>The Shadows slider's named ends.</summary>
    public IReadOnlyList<UiText> ShadowsEnds { get; } = [UiText.Number(TrainSettingsDef.MinShadows), UiText.Number(TrainSettingsDef.MaxShadows)];

    /// <summary>The Run length slider's named ends.</summary>
    public IReadOnlyList<UiText> RunLengthEnds { get; } = [Seconds(TrainSettingsDef.MinRunLengthSeconds), Seconds(TrainSettingsDef.MaxRunLengthSeconds)];

    public void SetMode(TrainingRunMode mode)
    {
        if (mode == TrainingRunMode.Simulate && !CanSimulate)
        {
            throw new InvalidOperationException("An untrained Creation has no network to simulate.");
        }

        if (mode == Mode)
        {
            return;
        }

        Mode = mode;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetShadows(double position) =>
        Apply(new TrainSettingsDef((int)ShadowsRange.ValueAt(position), Settings.RunLengthSeconds, Settings.RunLengthMode, Settings.RunLengthRange));

    /// <summary>
    /// Fixed or Ranged. The first Ranged range runs from the Fixed length to the longest; after
    /// that, each mode keeps its own value.
    /// </summary>
    public void SetRunLengthMode(RunLengthMode mode) =>
        Apply(new TrainSettingsDef(
            Settings.Shadows,
            Settings.RunLengthSeconds,
            mode,
            Settings.RunLengthRange ?? (mode == RunLengthMode.Ranged
                ? new RunLengthRangeDef(Settings.RunLengthSeconds, TrainSettingsDef.MaxRunLengthSeconds)
                : null)));

    /// <summary>
    /// Moves Run length's <paramref name="thumb"/>: Fixed has thumb 0, Ranged its low end 0 and high end 1.
    /// A Ranged end stops at the other, so each thumb keeps naming its own end.
    /// </summary>
    public void SetRunLength(int thumb, double position)
    {
        var seconds = (int)RunLengthRange.ValueAt(position);
        if (Settings is not { RunLengthMode: RunLengthMode.Ranged, RunLengthRange: { } range })
        {
            Apply(new TrainSettingsDef(Settings.Shadows, seconds, Settings.RunLengthMode, Settings.RunLengthRange));
            return;
        }

        var moved = thumb == 0
            ? new RunLengthRangeDef(Math.Min(seconds, range.HighSeconds), range.HighSeconds)
            : new RunLengthRangeDef(range.LowSeconds, Math.Max(seconds, range.LowSeconds));
        Apply(new TrainSettingsDef(Settings.Shadows, Settings.RunLengthSeconds, RunLengthMode.Ranged, moved));
    }

    private void Apply(TrainSettingsDef settings)
    {
        if (Mode == TrainingRunMode.Simulate)
        {
            throw new InvalidOperationException("Simulate has no Shadows, Run length or Run length mode to set.");
        }

        if (settings == Settings)
        {
            return;
        }

        Settings = settings;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static UiText Seconds(int seconds) => UiText.Format("{0} s", seconds);
}
