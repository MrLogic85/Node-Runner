using NodeRunner.Domain;

namespace NodeRunner.Creature;

/// <summary>
/// First concrete instance of the Node/Beam/Core model: a 5-node chain
/// (mirrors the shape of the old 0.1.0 worm) with a core mounted on the head
/// node. Nodes 1-3 each sit between two beams, giving 3 motorized
/// connections; the end nodes (0 and 4) have a single beam and stay passive.
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
            new NodeDef(new Vector2D(0, y), radius),
            new NodeDef(new Vector2D(spacing, y), radius),
            new NodeDef(new Vector2D(spacing * 2, y), radius),
            new NodeDef(new Vector2D(spacing * 3, y), radius),
            new NodeDef(new Vector2D(spacing * 4, y), radius),
        };

        var beams = new[]
        {
            new BeamDef(0, 1),
            new BeamDef(1, 2),
            new BeamDef(2, 3),
            new BeamDef(3, 4),
        };

        var cores = new[]
        {
            new CoreDef(0),
        };

        return new CreatureDef(nodes, beams, cores);
    }
}
