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
        roundTripped.Training.ShouldNotBeNull();
        roundTripped.Training.Brain.Neurons.ToArray().ShouldBe(original.Training!.Brain.Neurons.ToArray());
        roundTripped.Training.Brain.Connections.ToArray().ShouldBe(original.Training.Brain.Connections.ToArray());
        roundTripped.Training.Brain.NextNeuronId.ShouldBe(original.Training.Brain.NextNeuronId);
        roundTripped.Training.Generation.ShouldBe(original.Training.Generation);
        roundTripped.TrainSettings.ShouldBe(original.TrainSettings);
    }

    [Fact]
    public void WithHelpers_KeepTheTrainSettings()
    {
        var original = CreateCreation();

        original.WithName("Crawler").TrainSettings.ShouldBe(original.TrainSettings);
        original.WithTraining(null).TrainSettings.ShouldBe(original.TrainSettings);
        original.WithCreature(CreateCreature(), null).TrainSettings.ShouldBe(original.TrainSettings);
        original.CopyAs(Guid.NewGuid(), "Copy").TrainSettings.ShouldBe(original.TrainSettings);
        original.WithTrainSettings(new TrainSettingsDef(4, 30)).TrainSettings.ShouldBe(new TrainSettingsDef(4, 30));
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
            TestTraining.State(7, 1, TestTraining.Run),
            new TrainSettingsDef(12, 20));
    }

    private static CreatureDef CreateCreature()
    {
        return new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0))],
            [new BeamDef(101, 1, 2)],
            [new SensorDef(201, 101, SensorKind.Accelerometer)]);
    }
}
