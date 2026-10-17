using System.Text.Json;

namespace NodeRunner.Domain.Tests;

public sealed class CreationDefTests
{
    private static readonly CreationDef _creation = new(Guid.NewGuid(), "Worm", EveryPartKind.Creature, TestTraining.State(7, 1, TestTraining.Run), new TrainSettingsDef(12, 20));

    [Fact]
    public void JsonRoundTrip_KeepsTheCreatureTrainingAndSettings()
    {
        var loaded = JsonSerializer.Deserialize<CreationDef>(JsonSerializer.Serialize(_creation))!;

        (loaded.Id, loaded.Name, loaded.TrainSettings).ShouldBe((_creation.Id, "Worm", new TrainSettingsDef(12, 20)));
        loaded.Creature.Parts.ShouldBe(_creation.Creature.Parts);
        loaded.Training!.Network.Neurons.ShouldBe(_creation.Training!.Network.Neurons);
        loaded.Training.Network.Connections.ShouldBe(_creation.Training.Network.Connections);
        (loaded.Training.Network.NextNeuronId, loaded.Training.Generation, loaded.Training.Latest, loaded.Training.Best)
            .ShouldBe((3, 7, TestTraining.Run, _creation.Training.Best));
    }

    [Fact]
    public void WithHelpers_KeepTheTrainSettings_AndAnIdIsRequired()
    {
        _creation.WithName("Crawler").TrainSettings.ShouldBe(_creation.TrainSettings);
        _creation.WithTraining(null).TrainSettings.ShouldBe(_creation.TrainSettings);
        _creation.WithCreature(new CreatureDef([], [], []), null).TrainSettings.ShouldBe(_creation.TrainSettings);
        _creation.CopyAs(Guid.NewGuid(), "Copy").TrainSettings.ShouldBe(_creation.TrainSettings);
        _creation.WithTrainSettings(new TrainSettingsDef(4, 30)).TrainSettings.ShouldBe(new TrainSettingsDef(4, 30));
        Refuses.Invalid(() => new CreationDef(Guid.Empty, "Worm", EveryPartKind.Creature));
    }
}
