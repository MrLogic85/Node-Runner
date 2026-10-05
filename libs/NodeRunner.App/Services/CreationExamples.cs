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
    public static readonly Guid WalkerId = Guid.Parse("0d6c3c1e-2b7a-4f5e-9a51-7b1f6d2e8c40");

    public static CreationExample Walker { get; } = new(
        WalkerId,
        UiText.Plain("Walker"),
        UiText.Plain("Two crossed pistons swing two legs: a first walk."),
        CreateWalkerCreature());

    public static IReadOnlyList<CreationExample> All { get; } = [Walker];

    /// <summary>
    /// A walker seen from the side (#745): a back with a leg hanging from each end, and two Pistons
    /// crossing between them, each from one end of the back to the other leg's foot. Its joints are
    /// passive (#450), so the Pistons hold it up and swing the legs; the brain drives nothing else.
    /// The legs splay out a little, so it stands wider than its back.
    /// </summary>
    public static CreatureDef CreateWalkerCreature()
    {
        var nodes = new[]
        {
            new NodeDef(1, new Vector2D(0, -120)),
            new NodeDef(2, new Vector2D(200, -120)),
            new NodeDef(3, new Vector2D(-40, 0)),
            new NodeDef(4, new Vector2D(240, 0)),
        };

        var beams = new[]
        {
            new BeamDef(5, 1, 2),
            new BeamDef(6, 1, 3),
            new BeamDef(7, 2, 4),
        };

        var sensors = new[] { new SensorDef(8, 5, SensorKind.Accelerometer) };

        var pistons = new[]
        {
            new PistonDef(9, 2, 3),
            new PistonDef(10, 1, 4),
        };

        return new CreatureDef(nodes, beams, sensors, pistons, nextPartId: 11);
    }
}
