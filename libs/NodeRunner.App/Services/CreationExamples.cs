using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Services;

/// <summary>
/// A ready-made creation on the Examples screen and the one line on what it shows. Its name is
/// <see cref="UiText"/>, so a copy is saved under the name in the player's language.
/// </summary>
public sealed record CreationExample(Guid Id, UiText Name, UiText WhatIsNew, CreatureDef Creature, TrainingStateDef? Training = null);

/// <summary>
/// The examples the app ships with. Copying one saves a new creation; the example itself never changes.
/// </summary>
public static class CreationExamples
{
    public static readonly Guid WormId = Guid.Parse("17f2bd34-4f1b-46f1-a657-7e1e123d1390");

    public static CreationExample Worm { get; } = new(
        WormId,
        UiText.Plain("Worm"),
        UiText.Plain("A piston arches its back: an inchworm crawl."),
        CreateWormCreature());

    public static readonly Guid FrogId = Guid.Parse("5c0f7a2e-8d41-4b6a-9f3e-2a7d1c9b4e60");

    public static CreationExample Frog { get; } = new(
        FrogId,
        UiText.Plain("Frog"),
        UiText.Plain("Three pistons work one leg: the brain must time them together."),
        CreateFrogCreature());

    public static IReadOnlyList<CreationExample> All { get; } = [Worm, Frog];

    /// <summary>
    /// An inchworm: a flat tail and a high hump at the front, with a Piston under the hump. Pulling
    /// the Piston in raises the hump; pushing it out stretches the front forward. Its joints are
    /// passive (#450), so the Piston is the only thing the brain drives. The hump is high enough that
    /// the Piston's full stroke never flattens it, where a straight push could no longer bend it, and
    /// lopsided so the crawl has a forward direction.
    /// </summary>
    public static CreatureDef CreateWormCreature()
    {
        const double spacing = 90;
        const double y = 0;
        const double hump = -90;

        var nodes = new[]
        {
            new NodeDef(1, new Vector2D(0, y)),
            new NodeDef(2, new Vector2D(spacing, y)),
            new NodeDef(3, new Vector2D(spacing * 2, hump)),
            new NodeDef(4, new Vector2D(spacing * 3, y)),
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

    /// <summary>
    /// A frog seen from the side, facing right (#811): a rigid body triangle (hip, head, front foot)
    /// and a folded hind leg (hip, knee, heel, toe). Three Pistons work the leg: front foot to knee
    /// swings the thigh, hip to heel opens the knee, knee to toe turns the foot. Every joint is
    /// passive (#450), so the body only holds its shape while the Pistons push, and a hop needs all
    /// three at once.
    /// </summary>
    public static CreatureDef CreateFrogCreature()
    {
        var nodes = new[]
        {
            new NodeDef(1, new Vector2D(0, -180)),
            new NodeDef(2, new Vector2D(200, -190)),
            new NodeDef(3, new Vector2D(210, 0)),
            new NodeDef(4, new Vector2D(120, -35)),
            new NodeDef(5, new Vector2D(-50, -12)),
            new NodeDef(6, new Vector2D(40, 0)),
        };

        var beams = new[]
        {
            new BeamDef(7, 1, 2),
            new BeamDef(8, 2, 3),
            new BeamDef(9, 1, 3),
            new BeamDef(10, 1, 4),
            new BeamDef(11, 4, 5),
            new BeamDef(12, 5, 6),
        };

        var sensors = new[] { new SensorDef(13, 7, SensorKind.Accelerometer) };

        var pistons = new[]
        {
            new PistonDef(14, 3, 4),
            new PistonDef(15, 1, 5),
            new PistonDef(16, 4, 6),
        };

        return new CreatureDef(nodes, beams, sensors, pistons, nextPartId: 17);
    }
}
