using NodeRunner.Domain;

namespace NodeRunner.App.Services;

/// <summary>A ready-made creation on the Examples screen and the one line on what it shows.</summary>
public sealed record CreationExample(CreationDef Creation, string WhatIsNew);

/// <summary>
/// The examples the app ships with. Copying one saves a new creation; the example itself never changes.
/// </summary>
public static class CreationExamples
{
    public static readonly Guid WormId = Guid.Parse("17f2bd34-4f1b-46f1-a657-7e1e123d1390");

    public static IReadOnlyList<CreationExample> All { get; } =
    [
        new(new CreationDef(WormId, "Worm", CreateWormCreature()), "Beams and one core: the simplest crawl."),
    ];

    public static CreatureDef CreateWormCreature()
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

        var cores = new[]
        {
            new CoreDef(10, 1),
        };

        return new CreatureDef(nodes, beams, cores, nextPartId: 11);
    }
}
