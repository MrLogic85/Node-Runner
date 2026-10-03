using System.Globalization;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// Train setup (#194), between Build and Training: the Shadows and Run length sliders, filled from
/// the Creation's saved values or the default. Start saves <see cref="Settings"/> on the Creation
/// (#617). Simulate, the map choice and Run until power is out come later (#702, #540, 0.18).
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
            ? $"{CreationCardPresentation.FormatCount(training.Generation, "generation")} so far"
            : "Not trained yet";
        Settings = creation.TrainSettings ?? TrainSettingsDef.Default;
    }

    public event EventHandler? Changed;

    public Guid CreationId { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public TrainSettingsDef Settings { get; private set; }

    public PartSlider Shadows => new(
        "Shadows",
        Settings.Shadows.ToString(CultureInfo.InvariantCulture),
        ShadowsRange.Position(Settings.Shadows));

    public PartSlider RunLength => new("Run length", Seconds(Settings.RunLengthSeconds), RunLengthRange.Position(Settings.RunLengthSeconds));

    /// <summary>The Shadows slider's named ends.</summary>
    public string[] ShadowsEnds { get; } =
        [TrainSettingsDef.MinShadows.ToString(CultureInfo.InvariantCulture), TrainSettingsDef.MaxShadows.ToString(CultureInfo.InvariantCulture)];

    /// <summary>The Run length slider's named ends.</summary>
    public string[] RunLengthEnds { get; } = [Seconds(TrainSettingsDef.MinRunLengthSeconds), Seconds(TrainSettingsDef.MaxRunLengthSeconds)];

    public void SetShadows(double position) =>
        Apply(new TrainSettingsDef((int)ShadowsRange.ValueAt(position), Settings.RunLengthSeconds));

    public void SetRunLength(double position) =>
        Apply(new TrainSettingsDef(Settings.Shadows, (int)RunLengthRange.ValueAt(position)));

    private void Apply(TrainSettingsDef settings)
    {
        if (settings == Settings)
        {
            return;
        }

        Settings = settings;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string Seconds(int seconds) => $"{seconds.ToString(CultureInfo.InvariantCulture)} s";
}
