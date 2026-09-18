using System.Text.Json;

namespace NodeRunner.Domain.Tests;

public sealed class CreatureDefTests
{
    [Fact]
    public void Constructor_WithValidAnatomy_StoresParts()
    {
        var joints = new[]
        {
            new JointDef(new Vector2D(0, 0), 1),
            new JointDef(new Vector2D(2, 0), 1),
        };
        var bones = new[] { new BoneDef(0, 1) };
        var muscles = new[] { new MuscleDef(0, 1, 2, 10) };

        var creature = new CreatureDef(joints, bones, muscles);

        creature.Joints.ToArray().ShouldBe(joints);
        creature.Bones.ToArray().ShouldBe(bones);
        creature.Muscles.ToArray().ShouldBe(muscles);
    }

    [Fact]
    public void Constructor_WithTooFewJoints_Throws()
    {
        var action = () => new CreatureDef(
            new[] { new JointDef(new Vector2D(0, 0), 1) },
            [],
            []);

        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithOutOfRangeBoneJoint_Throws()
    {
        var action = () => new CreatureDef(
            new[]
            {
                new JointDef(new Vector2D(0, 0), 1),
                new JointDef(new Vector2D(2, 0), 1),
            },
            new[] { new BoneDef(0, 2) },
            []);

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithOutOfRangeMuscleJoint_Throws()
    {
        var action = () => new CreatureDef(
            new[]
            {
                new JointDef(new Vector2D(0, 0), 1),
                new JointDef(new Vector2D(2, 0), 1),
            },
            [],
            new[] { new MuscleDef(0, 2, 2, 10) });

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void JsonRoundTrip_PreservesCreatureDefinition()
    {
        var original = new CreatureDef(
            new[]
            {
                new JointDef(new Vector2D(0, 0), 1),
                new JointDef(new Vector2D(2, 0), 1.5),
            },
            new[] { new BoneDef(0, 1) },
            new[] { new MuscleDef(0, 1, 2, 10) });

        var json = JsonSerializer.Serialize(original);

        var roundTripped = JsonSerializer.Deserialize<CreatureDef>(json);

        roundTripped.ShouldNotBeNull();
        roundTripped.Joints.ToArray().ShouldBe(original.Joints.ToArray());
        roundTripped.Bones.ToArray().ShouldBe(original.Bones.ToArray());
        roundTripped.Muscles.ToArray().ShouldBe(original.Muscles.ToArray());
    }

    [Fact]
    public void Constructor_DefensivelyCopiesInputCollections()
    {
        var joints = new[]
        {
            new JointDef(new Vector2D(0, 0), 1),
            new JointDef(new Vector2D(2, 0), 1),
        };
        var bones = new[] { new BoneDef(0, 1) };
        var muscles = new[] { new MuscleDef(0, 1, 2, 10) };

        var creature = new CreatureDef(joints, bones, muscles);
        joints[0] = new JointDef(new Vector2D(99, 99), 1);
        bones[0] = new BoneDef(1, 0);
        muscles[0] = new MuscleDef(1, 0, 3, 11);

        creature.Joints[0].Position.ShouldBe(new Vector2D(0, 0));
        creature.Bones[0].ShouldBe(new BoneDef(0, 1));
        creature.Muscles[0].ShouldBe(new MuscleDef(0, 1, 2, 10));
    }
}
