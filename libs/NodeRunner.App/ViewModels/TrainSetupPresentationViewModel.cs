using NodeRunner.App.Navigation;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// One stepped slider row: its label, its readout, its thumb at 0…1 and the distance between two
/// whole steps on that scale, so the thumb stops only where the value does (#711). A disabled row
/// is shown dimmed and cannot be dragged. A null <paramref name="Position"/> shows no thumb, for a
/// value the scale does not hold.
/// </summary>
public sealed record SettingSlider(string Label, UiText Readout, double? Position, double Step, bool Disabled = false);

/// <summary>
/// Train setup (#194), between Build and Training: Train or Simulate, and the Shadows and Run length
/// sliders, filled from the Creation's saved values or the default. Training starts by saving
/// <see cref="Settings"/> on the Creation (#617). Simulate (#702) needs a trained Creation; it plays
/// one shadow until the player leaves, so it dims both sliders and saves nothing. The selected map
/// card names <see cref="Maps.Default"/> (#444). The map choice and Run until power is out come
/// later (#540, 0.18).
/// </summary>
public sealed class TrainSetupPresentationViewModel
{
    public static SettingRange ShadowsRange { get; } = new(TrainSettingsDef.MinShadows, TrainSettingsDef.MaxShadows, 1);

    public static SettingRange RunLengthRange { get; } = new(TrainSettingsDef.MinRunLengthSeconds, TrainSettingsDef.MaxRunLengthSeconds, 5);

    public TrainSetupPresentationViewModel(CreationDef creation)
    {
        ArgumentNullException.ThrowIfNull(creation);
        CreationId = creation.Id;
        Title = $"Train {creation.Name}";
        Subtitle = creation.Training is { } training
            ? UiText.Counted("{0} generation so far", "{0} generations so far", training.Generation)
            : UiText.Plain("Not trained yet");
        Settings = creation.TrainSettings ?? TrainSettingsDef.Default;
        CanSimulate = creation.Training is not null;
    }

    public event EventHandler? Changed;

    public Guid CreationId { get; }

    public string Title { get; }

    public UiText Subtitle { get; }

    public string MapName => Maps.Default.Name;

    public TrainSettingsDef Settings { get; private set; }

    public TrainingRunMode Mode { get; private set; }

    /// <summary>Whether Simulate can be chosen: only a trained Creation has a brain to play.</summary>
    public bool CanSimulate { get; }

    /// <summary>The one line under Train or Simulate saying what the chosen mode does.</summary>
    public string ModeNote => Mode switch
    {
        TrainingRunMode.Simulate => "Plays the trained brain with one shadow. Nothing is learned or saved.",
        _ when CanSimulate => "Shadows race and the brain keeps learning.",
        _ => "Shadows race and the brain keeps learning. Simulate needs a trained brain.",
    };

    public SettingSlider Shadows => Mode == TrainingRunMode.Simulate
        ? new("Shadows", UiText.Number(1), null, ShadowsRange.PositionStep, Disabled: true)
        : new(
            "Shadows",
            UiText.Number(Settings.Shadows),
            ShadowsRange.Position(Settings.Shadows),
            ShadowsRange.PositionStep);

    public SettingSlider RunLength => Mode == TrainingRunMode.Simulate
        ? new("Run length", UiText.Plain("Until you leave"), null, RunLengthRange.PositionStep, Disabled: true)
        : new(
            "Run length",
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
            throw new InvalidOperationException("An untrained Creation has no brain to simulate.");
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
