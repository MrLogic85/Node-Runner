using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BrainPortLabelsTests
{
    [Fact]
    public void For_NamesEachPortByItsPartAndReading_InPortOrder()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0), "Knee"), new NodeDef(3, new Vector2D(4, 0))],
            [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)],
            [new SensorDef(6, 4, SensorKind.Accelerometer), new SensorDef(7, 5, SensorKind.Camera, "Eye")],
            [new PistonDef(8, 1, 3, "Ram")]);

        var labels = BrainPortLabels.For(creature);

        labels.Inputs.ShouldBe(["Accelerometer: along", "Accelerometer: across", "Eye: left 1", "Eye: centre", "Eye: right 1", "Ram: length", "Ram: speed"]);
        labels.Outputs.ShouldBe(["Ram: position", "Ram: strength"]);
        labels.Inputs.Count.ShouldBe(BrainPorts.Of(creature).Inputs.Count);
    }

    [Fact]
    public void For_APiston_NamesItsTwoInputsAndTwoOutputs()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0))],
            [],
            [],
            [new PistonDef(3, 1, 2, "Ram")]);

        var labels = BrainPortLabels.For(creature);

        labels.Inputs.ShouldBe(["Ram: length", "Ram: speed"]);
        labels.Outputs.ShouldBe(["Ram: position", "Ram: strength"]);
    }
}
