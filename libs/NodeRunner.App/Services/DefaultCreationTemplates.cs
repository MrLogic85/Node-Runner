using NodeRunner.Domain;

namespace NodeRunner.App.Services;

public static class DefaultCreationTemplates
{
    public static readonly Guid StarterWormId = Guid.Parse("17f2bd34-4f1b-46f1-a657-7e1e123d1390");

    public static CreationDef CreateStarterWorm()
    {
        return new CreationDef(StarterWormId, "Example: Worm", CreateStarterWormCreature());
    }

    public static CreatureDef CreateStarterWormCreature()
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
