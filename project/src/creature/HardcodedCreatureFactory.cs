using NodeRunner.Domain;

namespace NodeRunner.Creature;

/// <summary>
/// First concrete instance of the node/beam/sensor model: a 5-node chain
/// (mirrors the shape of the old 0.1.0 worm) with an accelerometer and an LOS
/// sensor on the head beam. Nodes 1-3 each sit between two beams, giving 3
/// motorized connections; the end nodes (0 and 4) have a single beam and stay
/// passive.
/// </summary>
public static class HardcodedCreatureFactory
{
    public static CreatureDef Create()
    {
        const double radius = 18;
        const double spacing = 56;
        const double y = 0;

        var nodes = new[]
        {
            new NodeDef(1, new Vector2D(0, y), radius),
            new NodeDef(2, new Vector2D(spacing, y), radius),
            new NodeDef(3, new Vector2D(spacing * 2, y), radius),
            new NodeDef(4, new Vector2D(spacing * 3, y), radius),
            new NodeDef(5, new Vector2D(spacing * 4, y), radius),
        };

        var beams = new[]
        {
            new BeamDef(6, 1, 2),
            new BeamDef(7, 2, 3),
            new BeamDef(8, 3, 4),
            new BeamDef(9, 4, 5),
        };

        var sensors = new[]
        {
            new SensorDef(10, 6, SensorKind.Accelerometer),
            new SensorDef(11, 6, SensorKind.LineOfSight),
        };

        return new CreatureDef(nodes, beams, sensors, nextPartId: 12);
    }
}
