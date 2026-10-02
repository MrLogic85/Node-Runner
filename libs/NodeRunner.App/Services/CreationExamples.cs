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
        new(new CreationDef(WormId, "Worm", CreateWormCreature()), "A piston arches its back: an inchworm crawl."),
    ];

    /// <summary>
    /// An inchworm: a flat tail and a high hump at the front, with a Piston under the hump. Pulling
    /// the Piston in raises the hump; pushing it out stretches the front forward. Its joints are
    /// passive (#450), so the Piston is the only thing the brain drives. The hump is high enough that
    /// the Piston's full stroke never flattens it, where a straight push could no longer bend it, and
    /// lopsided so the crawl has a forward direction.
    /// </summary>
    public static CreatureDef CreateWormCreature()
    {
        const double radius = 18;
        const double spacing = 90;
        const double y = 0;
        const double hump = -90;

        var nodes = new[]
        {
            new NodeDef(1, new Vector2D(0, y), radius),
            new NodeDef(2, new Vector2D(spacing, y), radius),
            new NodeDef(3, new Vector2D(spacing * 2, hump), radius),
            new NodeDef(4, new Vector2D(spacing * 3, y), radius),
        };

        var beams = new[]
        {
            new BeamDef(5, 1, 2),
            new BeamDef(6, 2, 3),
            new BeamDef(7, 3, 4),
        };

        var sensors = new[]
        {
            new SensorDef(8, 5, SensorKind.Accelerometer),
            new SensorDef(9, 7, SensorKind.Camera),
        };

        var pistons = new[] { new PistonDef(10, 2, 4) };

        return new CreatureDef(nodes, beams, sensors, pistons, nextPartId: 11);
    }
}
