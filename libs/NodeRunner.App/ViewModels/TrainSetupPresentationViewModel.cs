using NodeRunner.App.Navigation;
using NodeRunner.App.Services;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// One stepped slider row: its label, its readout, its thumb at 0…1 and the distance between two
/// whole steps on that scale, so the thumb stops only where the value does. A disabled row
/// is shown dimmed and cannot be dragged. A null <paramref name="Position"/> shows no thumb, for a
/// value the scale does not hold.
/// </summary>
public sealed record SettingSlider(UiText Label, UiText Readout, double? Position, double Step, bool Disabled = false);

/// <summary>
/// The line under the Shadows slider: a quiet note while <see cref="ShadowsLoad.Smooth"/>, else a
/// warning whose colour rises with the load.
/// </summary>
public sealed record SettingNote(UiText Text, ShadowsLoad Load);

/// <summary>
/// Train setup, between Build and Training: Train or Simulate, and the Shadows and Run length
/// sliders, filled from the Creation's saved values or the default. Training starts by saving
/// <see cref="Settings"/> on the Creation. Simulate needs a trained Creation, so an
/// untrained one has no Train or Simulate choice at all; it plays one shadow until the player
/// leaves, so it dims both sliders and saves nothing. The selected map
/// card names <see cref="Maps.Default"/>. The map choice and Run until power is out come
/// later (#540).
/// </summary>
public sealed class TrainSetupPresentationViewModel
{
    public static SettingRange ShadowsRange { get; } = new(TrainSettingsDef.MinShadows, TrainSettingsDef.MaxShadows, 1);

    public static SettingRange RunLengthRange { get; } = new(TrainSettingsDef.MinRunLengthSeconds, TrainSettingsDef.MaxRunLengthSeconds, 5);

    public TrainSetupPresentationViewModel(CreationDef creation)
    {
        ArgumentNullException.ThrowIfNull(creation);
        CreationId = creation.Id;
        Title = UiText.Format("Train {0}", creation.Name);
        Subtitle = creation.Training is { } training
            ? UiText.Counted("{0} generation so far", "{0} generations so far", training.Generation)
            : UiText.Plain("Not trained yet");
        Settings = creation.TrainSettings ?? TrainSettingsDef.Default;
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
        : new(
            UiText.Plain("Run length"),
            Seconds(Settings.RunLengthSeconds),
            RunLengthRange.Position(Settings.RunLengthSeconds),
            RunLengthRange.PositionStep);

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
        Apply(new TrainSettingsDef((int)ShadowsRange.ValueAt(position), Settings.RunLengthSeconds));

    public void SetRunLength(double position) =>
        Apply(new TrainSettingsDef(Settings.Shadows, (int)RunLengthRange.ValueAt(position)));

    private void Apply(TrainSettingsDef settings)
    {
        if (Mode == TrainingRunMode.Simulate)
        {
            throw new InvalidOperationException("Simulate has no Shadows or Run length to set.");
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
