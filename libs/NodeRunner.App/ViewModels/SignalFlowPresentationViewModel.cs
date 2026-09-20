using System.ComponentModel;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>Presentation state for the four-stage SignalFlow teaching surface.</summary>
public sealed class SignalFlowPresentationViewModel : INotifyPropertyChanged
{
    private static readonly SignalFlowReadingPresentation[] _emptyReadings = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<SignalFlowReadingPresentation> SensorRows { get; private set; } = _emptyReadings;

    public IReadOnlyList<SignalFlowReadingPresentation> MotorRows { get; private set; } = _emptyReadings;

    public int SensorCount { get; private set; }

    public int MotorCount { get; private set; }

    public string SeesSummary { get; private set; } = "Waiting for live sensors";

    public string DecidesSummary { get; private set; } = "Brain waits for sensor input";

    public string TwistsSummary { get; private set; } = "Waiting for motor targets";

    public string ScoresSummary { get; private set; } = "Distance updates after each try";

    public void Update(
        IReadOnlyList<SensorReading> sensors,
        IReadOnlyList<MotorReading> motors,
        double bestFitness,
        double meanFitness)
    {
        ArgumentNullException.ThrowIfNull(sensors);
        ArgumentNullException.ThrowIfNull(motors);

        SensorCount = sensors.Count;
        MotorCount = motors.Count;
        SensorRows = sensors.Take(3).Select(ToSensorRow).ToArray();
        MotorRows = motors.Take(2).Select(ToMotorRow).ToArray();
        SeesSummary = sensors.Count == 0
            ? "No sensors are active yet"
            : $"{sensors.Count} live sensor values";
        DecidesSummary = sensors.Count == 0 || motors.Count == 0
            ? "Brain waits for a complete body"
            : $"Brain maps {sensors.Count} inputs to {motors.Count} motor targets";
        TwistsSummary = motors.Count == 0
            ? "No motor relations yet"
            : $"{motors.Count} live motor targets";
        ScoresSummary = double.IsNegativeInfinity(bestFitness)
            ? $"Mean {meanFitness:0.0} m · best pending"
            : $"Best {bestFitness:0.0} m · mean {meanFitness:0.0} m";

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    private static SignalFlowReadingPresentation ToSensorRow(SensorReading sensor)
    {
        var label = $"{sensor.GroupKind} {sensor.GroupIndex} {sensor.Name}";
        return new SignalFlowReadingPresentation(label, $"{sensor.Value:0.00}", Normalize(sensor.Value));
    }

    private static SignalFlowReadingPresentation ToMotorRow(MotorReading motor)
    {
        var label = $"Motor relation {motor.GroupIndex}";
        return new SignalFlowReadingPresentation(label, $"{motor.Target:0.00}", Normalize(motor.Target));
    }

    private static double Normalize(double value) => Math.Clamp(Math.Abs(value), 0, 1);
}
