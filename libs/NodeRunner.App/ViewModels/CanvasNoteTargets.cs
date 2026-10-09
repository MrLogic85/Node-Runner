using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

public static class CanvasNoteTargets
{
    public static IReadOnlyList<int> JointIds(CreatureElementSelection target, BuildViewModel build)
    {
        ArgumentNullException.ThrowIfNull(build);
        return target.Kind switch
        {
            CreatureElementKind.Node => [target.Id],
            CreatureElementKind.Servo => [build.Servos[build.ServoIndexOf(target.Id)].NodeId],
            CreatureElementKind.Wheel => [build.Wheels[build.WheelIndexOf(target.Id)].NodeId],
            CreatureElementKind.Beam or CreatureElementKind.Piston or CreatureElementKind.Spring => LinkJoints(build.Link(target.Id)),
            CreatureElementKind.Sensor => SensorJoints(target.Id, build),
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
    }

    private static int[] SensorJoints(int sensorId, BuildViewModel build)
    {
        var sensor = build.Sensors.First(entry => entry.Id == sensorId);
        var beam = build.Beams[build.BeamIndexOf(sensor.BeamId)];
        return [beam.NodeA, beam.NodeB];
    }

    private static int[] LinkJoints(LinkRef link) => [link.NodeA, link.NodeB];
}
