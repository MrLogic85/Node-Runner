using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BrainPortLabelsTests
{
    [Fact]
    public void For_NamesEachPortByItsPartAndReading_InPortOrder()
    {
        // Node 2 joins beams 4 and 5, so its motor turns beam 5.
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 1), new NodeDef(2, new Vector2D(2, 0), 1, "Knee"), new NodeDef(3, new Vector2D(4, 0), 1)],
            [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)],
            [new SensorDef(6, 4, SensorKind.Accelerometer), new SensorDef(7, 5, SensorKind.Camera, "Eye")]);

        var labels = BrainPortLabels.For(creature);

        labels.Inputs.ShouldBe(["Knee: angle", "Knee: speed", "Accelerometer: along", "Accelerometer: across", "Eye: left 1", "Eye: centre", "Eye: right 1"]);
        labels.Outputs.ShouldBe(["Knee"]);
        labels.Inputs.Count.ShouldBe(BrainPorts.Of(creature).Inputs.Count);
    }

    [Fact]
    public void For_AJointWithTwoMotors_AddsTheBeamEachTurns()
    {
        // Node 1 holds three beams: beam 4 is its reference, beams 5 and 6 each get a motor.
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 1), new NodeDef(2, new Vector2D(2, 0), 1), new NodeDef(3, new Vector2D(0, 2), 1), new NodeDef(7, new Vector2D(-2, 0), 1)],
            [new BeamDef(4, 1, 2), new BeamDef(5, 1, 3), new BeamDef(6, 1, 7)],
            []);

        var labels = BrainPortLabels.For(creature);

        labels.Outputs.ShouldBe(["Node 1 · Beam 2", "Node 1 · Beam 3"]);
        labels.Inputs[0].ShouldBe("Node 1 · Beam 2: angle");
    }
}
