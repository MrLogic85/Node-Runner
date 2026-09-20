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
        roundTripped.Creature.Cores.ToArray().ShouldBe(original.Creature.Cores.ToArray());
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

    private static CreationDef CreateCreation()
    {
        return new CreationDef(
            Guid.NewGuid(),
            "Worm",
            CreateCreature(),
            new TrainingStateDef([2, 3, 1], [0.1, -0.2, 0.3], 7, "Tanh"));
    }

    private static CreatureDef CreateCreature()
    {
        return new CreatureDef(
            [new NodeDef(new Vector2D(0, 0), 1), new NodeDef(new Vector2D(2, 0), 1)],
            [new BeamDef(0, 1)],
            [new CoreDef(0)]);
    }
}
