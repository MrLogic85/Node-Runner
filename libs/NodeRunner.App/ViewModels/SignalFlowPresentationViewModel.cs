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

    /// <summary>How many readings the brain senses, such as "9 readings"; empty with none.</summary>
    public string SensesNote { get; private set; } = string.Empty;

    /// <summary>How many outputs the brain drives, such as "2 motors"; empty with none.</summary>
    public string OutputsNote { get; private set; } = string.Empty;

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

        var sensesNote = Count(sensors.Count, "reading");
        var outputsNote = Count(motors.Count, "motor");
        var distanceNote = double.IsFinite(distance)
            ? Metres.FormatWithUnit(distance)
            : string.Empty;
        if (SensesNote == sensesNote && OutputsNote == outputsNote && DistanceNote == distanceNote)
        {
            return;
        }

        SensesNote = sensesNote;
        OutputsNote = outputsNote;
        DistanceNote = distanceNote;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    private static string Count(int count, string noun) => count switch
    {
        0 => string.Empty,
        1 => $"1 {noun}",
        _ => $"{count} {noun}s",
    };
}
