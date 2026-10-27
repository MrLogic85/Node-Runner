using System.Text.Json;

namespace NodeRunner.Domain.Tests;

public sealed class CreationDefTests
{
    private static readonly CreationDef _creation = new(Guid.NewGuid(), "Worm", EveryPartKind.Creature, TestTraining.State(7, 1, TestTraining.Run), new TrainSettingsDef(12, 20))
    {
        Grid = new BuildGridDef(25, snap: true),
    };

    [Fact]
    public void JsonRoundTrip_KeepsTheCreatureTrainingSettingsAndGrid()
    {
        var loaded = JsonSerializer.Deserialize<CreationDef>(JsonSerializer.Serialize(_creation))!;

        (loaded.Id, loaded.Name, loaded.TrainSettings, loaded.Grid)
            .ShouldBe((_creation.Id, "Worm", new TrainSettingsDef(12, 20), new BuildGridDef(25, snap: true)));
        loaded.Creature.Parts.ShouldBe(_creation.Creature.Parts);
        loaded.Training!.Network.Neurons.ShouldBe(_creation.Training!.Network.Neurons);
        loaded.Training.Network.Connections.ShouldBe(_creation.Training.Network.Connections);
        (loaded.Training.Network.NextNeuronId, loaded.Training.Generation, loaded.Training.Latest, loaded.Training.Best)
            .ShouldBe((3, 7, TestTraining.Run, _creation.Training.Best));
    }

    [Fact]
    public void WithHelpers_KeepTheTrainSettingsAndGrid_AndAnIdIsRequired()
    {
        (_creation.WithName("Crawler").TrainSettings, _creation.WithName("Crawler").Grid).ShouldBe((_creation.TrainSettings, _creation.Grid));
        (_creation.WithTraining(null).TrainSettings, _creation.WithTraining(null).Grid).ShouldBe((_creation.TrainSettings, _creation.Grid));
        var reshaped = _creation.WithCreature(new CreatureDef([], [], []), null);
        (reshaped.TrainSettings, reshaped.Grid).ShouldBe((_creation.TrainSettings, _creation.Grid));
        var copy = _creation.CopyAs(Guid.NewGuid(), "Copy");
        (copy.TrainSettings, copy.Grid).ShouldBe((_creation.TrainSettings, _creation.Grid));
        var retrained = _creation.WithTrainSettings(new TrainSettingsDef(4, 30));
        (retrained.TrainSettings, retrained.Grid).ShouldBe((new TrainSettingsDef(4, 30), _creation.Grid));
        new CreationDef(Guid.NewGuid(), "Worm", EveryPartKind.Creature).Grid.ShouldBe(BuildGridDef.Default);
        Refuses.Invalid(() => new CreationDef(Guid.Empty, "Worm", EveryPartKind.Creature));
    }
}
