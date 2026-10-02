using System.Text.Json;

namespace NodeRunner.Domain.Tests;

public sealed class CreationDefTests
{
    [Fact]
    public void JsonRoundTrip_PreservesAnatomyAndTraining()
    {
        var original = CreateCreation();

        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<CreationDef>(json);

        roundTripped.ShouldNotBeNull();
        roundTripped.Id.ShouldBe(original.Id);
        roundTripped.Name.ShouldBe(original.Name);
        roundTripped.Creature.Nodes.ToArray().ShouldBe(original.Creature.Nodes.ToArray());
        roundTripped.Creature.Beams.ToArray().ShouldBe(original.Creature.Beams.ToArray());
        roundTripped.Creature.Sensors.ToArray().ShouldBe(original.Creature.Sensors.ToArray());
        roundTripped.BrainShape.ShouldBe(original.BrainShape);
        roundTripped.Training.ShouldNotBeNull();
        roundTripped.Training.LayerSizes.ShouldBe(original.Training!.LayerSizes);
        roundTripped.Training.BestGenome.ShouldBe(original.Training.BestGenome);
        roundTripped.Training.Generation.ShouldBe(original.Training.Generation);
        roundTripped.Training.Activation.ShouldBe(original.Training.Activation);
    }

    [Fact]
    public void Constructor_WithEmptyId_Throws()
    {
        var action = () => new CreationDef(Guid.Empty, "Worm", CreateCreature());

        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithoutBrainShape_Throws()
    {
        var action = () => new CreationDef(Guid.NewGuid(), "Worm", CreateCreature(), brainShape: null!);

        action.ShouldThrow<ArgumentNullException>();
    }

    private static CreationDef CreateCreation()
    {
        return new CreationDef(
            Guid.NewGuid(),
            "Worm",
            CreateCreature(),
            new BrainShapeDef(2, 5),
            new TrainingStateDef([2, 3, 1], [0.1, -0.2, 0.3], 7, "Tanh", 1, TestTraining.Run));
    }

    private static CreatureDef CreateCreature()
    {
        return new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 1), new NodeDef(2, new Vector2D(2, 0), 1)],
            [new BeamDef(101, 1, 2)],
            [new SensorDef(201, 101, SensorKind.Accelerometer)]);
    }
}
