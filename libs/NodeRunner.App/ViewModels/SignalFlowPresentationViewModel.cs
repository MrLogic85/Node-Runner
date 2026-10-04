using System.ComponentModel;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The SignalFlow column's four stages at rest: 1 Senses, 2 Brain, 3 Outputs, 4 Distance. Each
/// stage shows a short note; the pictures on the cards come with #196.
/// </summary>
public sealed class SignalFlowPresentationViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>How many readings the brain senses, such as "9 readings"; null with none.</summary>
    public UiText? SensesNote { get; private set; }

    /// <summary>The parts the brain drives, such as "1 piston" or "2 pistons"; null with none.</summary>
    public UiText? OutputsNote { get; private set; }

    /// <summary>How far the visible creature has got in this try, such as "12.4 m"; empty when no try runs.</summary>
    public string DistanceNote { get; private set; } = string.Empty;

    /// <summary>Refreshes the stage notes from this tick's readings.</summary>
    /// <param name="sensors">The visible creature's sensor readings.</param>
    /// <param name="motors">The visible creature's motor readings.</param>
    /// <param name="distance">How far the visible creature has got, in world units; NaN when no try runs.</param>
    public void Update(IReadOnlyList<SensorReading> sensors, IReadOnlyList<MotorReading> motors, double distance)
    {
        ArgumentNullException.ThrowIfNull(sensors);
        ArgumentNullException.ThrowIfNull(motors);

        var sensesNote = sensors.Count > 0 ? UiText.Counted("{0} reading", "{0} readings", sensors.Count) : null;
        var outputsNote = PartsNote(motors);
        var distanceNote = double.IsFinite(distance)
            ? Metres.FormatWithUnit(distance)
            : string.Empty;
        if (Equals(SensesNote, sensesNote) && Equals(OutputsNote, outputsNote) && DistanceNote == distanceNote)
        {
            return;
        }

        // The distance changes every tick and the counts rarely, so a distance-only change says so.
        var onlyDistance = Equals(SensesNote, sensesNote) && Equals(OutputsNote, outputsNote);
        SensesNote = sensesNote;
        OutputsNote = outputsNote;
        DistanceNote = distanceNote;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(onlyDistance ? nameof(DistanceNote) : null));
    }

    // A Piston drives two outputs, so the note counts parts, not outputs. Pistons are the only
    // output part; a new kind needs its own counted text and a join here.
    private static UiText? PartsNote(IReadOnlyList<MotorReading> motors)
    {
        System.Diagnostics.Debug.Assert(
            motors.All(motor => motor.GroupKind == MotorReading.PistonKind),
            "Every output part kind needs its counted text in the Outputs note.");
        var pistons = motors.Where(motor => motor.GroupKind == MotorReading.PistonKind)
            .Select(motor => motor.GroupIndex)
            .Distinct()
            .Count();
        return pistons > 0 ? UiText.Counted("{0} piston", "{0} pistons", pistons) : null;
    }
}
