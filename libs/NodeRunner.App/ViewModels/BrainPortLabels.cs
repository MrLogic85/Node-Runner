using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// What the brain's ports are called, in port order (<see cref="BrainPorts"/>): a sense is its
/// part's name and reading, such as "Accelerometer: along" or "Front knee: speed", and an output
/// is the joint it drives, such as "Rear knee". A joint with more than one motor adds the beam
/// each one turns.
/// </summary>
public sealed record BrainPortLabels(IReadOnlyList<string> Inputs, IReadOnlyList<string> Outputs)
{
    public static BrainPortLabels Empty { get; } = new([], []);

    public static BrainPortLabels For(CreatureDef creature)
    {
        ArgumentNullException.ThrowIfNull(creature);

        string Name(int partId) => PartNames.Display(creature.Nodes, creature.Beams, creature.Sensors, partId);

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

        var motors = MotorTopology.BuildNodeConnections(creature)
            .Where(connection => connection.IsMotorized)
            .Select(connection => (NodeId: creature.Nodes[connection.NodeIndex].Id, BeamId: creature.Beams[connection.OtherBeamIndex].Id))
            .ToArray();
        foreach (var (nodeId, beamId) in motors)
        {
            var motor = motors.Count(other => other.NodeId == nodeId) > 1
                ? $"{Name(nodeId)} · {Name(beamId)}"
                : Name(nodeId);
            var inputs = BrainPorts.JointMotorInputs(nodeId, beamId).ToArray();
            labels[inputs[0]] = $"{motor}: angle";
            labels[inputs[1]] = $"{motor}: speed";
            labels[BrainPorts.JointMotorOutput(nodeId, beamId)] = motor;
        }

        var layout = BrainPorts.Of(creature);
        return new BrainPortLabels(
            layout.Inputs.Select(port => labels[port]).ToArray(),
            layout.Outputs.Select(port => labels[port]).ToArray());
    }
}
