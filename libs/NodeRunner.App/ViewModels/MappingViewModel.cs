using System.ComponentModel;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// Formats live sensor and motor-relation readings for the sensor-to-brain-
/// to-motor mapping display (issue #42). Pure C# so it can be unit-tested
/// without a Creature/Godot node — the Godot side (project/src/creature)
/// reads the live values and calls <see cref="Update"/> once per frame.
/// </summary>
public sealed class MappingViewModel : INotifyPropertyChanged
{
    private const string _noSensorsText = "No sensors yet.";
    private const string _noMotorsText = "No motor relations yet.";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string SensorsText { get; private set; } = _noSensorsText;

    public string OutputsText { get; private set; } = _noMotorsText;

    public void Update(IReadOnlyList<SensorReading> sensors, IReadOnlyList<MotorReading> motors)
    {
        ArgumentNullException.ThrowIfNull(sensors);
        ArgumentNullException.ThrowIfNull(motors);

        SensorsText = sensors.Count == 0 ? _noSensorsText : FormatSensors(sensors);
        OutputsText = motors.Count == 0 ? _noMotorsText : FormatMotors(motors);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    private static string FormatSensors(IReadOnlyList<SensorReading> sensors)
    {
        var lines = new string[sensors.Count];
        for (var i = 0; i < sensors.Count; i++)
        {
            var sensor = sensors[i];
            lines[i] = $"{sensor.GroupKind} {sensor.GroupIndex} {sensor.Name}: {sensor.Value:0.00}";
        }

        return string.Join('\n', lines);
    }

    private static string FormatMotors(IReadOnlyList<MotorReading> motors)
    {
        var lines = new string[motors.Count];
        for (var i = 0; i < motors.Count; i++)
        {
            var motor = motors[i];
            lines[i] = $"Motor relation {motor.GroupIndex} \u2192 target {motor.Target:0.00}, torque {motor.Torque:0}";
        }

        return string.Join('\n', lines);
    }
}
