using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// What the brain's ports are called, in port order (<see cref="BrainPorts"/>): a sense is its
/// part's name and reading, such as "Accelerometer: along", and a Piston's ports are its name and
/// channel, such as "Piston 1: length" or "Piston 1: position".
/// </summary>
public sealed record BrainPortLabels(IReadOnlyList<string> Inputs, IReadOnlyList<string> Outputs)
{
    public static BrainPortLabels Empty { get; } = new([], []);

    public static BrainPortLabels For(CreatureDef creature)
    {
        ArgumentNullException.ThrowIfNull(creature);

        string Name(int partId) => PartNames.Display(creature.Nodes, creature.Beams, creature.Sensors, creature.Pistons, partId);

        var labels = new Dictionary<BrainPort, string>();
        foreach (var sensor in creature.Sensors)
        {
            var readings = sensor.Kind switch
            {
                SensorKind.Accelerometer => Accelerometer.ReadingNames,
                SensorKind.Camera => CameraRays.RayNames,
                _ => throw new InvalidOperationException($"Unknown sensor kind {sensor.Kind}."),
            };
            var ports = BrainPorts.SensorPorts(sensor).ToArray();
            for (var index = 0; index < ports.Length; index++)
            {
                labels[ports[index]] = $"{Name(sensor.Id)}: {readings[index]}";
            }
        }

        foreach (var piston in creature.Pistons)
        {
            var inputs = BrainPorts.PistonInputs(piston.Id).ToArray();
            var outputs = BrainPorts.PistonOutputs(piston.Id).ToArray();
            labels[inputs[0]] = $"{Name(piston.Id)}: length";
            labels[inputs[1]] = $"{Name(piston.Id)}: speed";
            labels[outputs[0]] = $"{Name(piston.Id)}: position";
            labels[outputs[1]] = $"{Name(piston.Id)}: strength";
        }

        var layout = BrainPorts.Of(creature);
        return new BrainPortLabels(
            layout.Inputs.Select(port => labels[port]).ToArray(),
            layout.Outputs.Select(port => labels[port]).ToArray());
    }
}
