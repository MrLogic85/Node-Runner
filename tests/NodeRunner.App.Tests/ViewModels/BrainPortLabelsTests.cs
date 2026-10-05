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

        var accelerometer = UiText.Plain("Accel");
        var eye = UiText.AsWritten("Eye");
        var ram = UiText.AsWritten("Ram");
        labels.Inputs.ShouldBe(
        [
            UiText.Format("{0}:\u00A0along", accelerometer),
            UiText.Format("{0}:\u00A0across", accelerometer),
            UiText.Format("{0}:\u00A0left", eye),
            UiText.Format("{0}:\u00A0centre", eye),
            UiText.Format("{0}:\u00A0right", eye),
            UiText.Format("{0}:\u00A0length", ram),
            UiText.Format("{0}:\u00A0speed", ram),
        ]);
        labels.Outputs.ShouldBe([UiText.Format("{0}:\u00A0length", ram), UiText.Format("{0}:\u00A0strength", ram)]);
        labels.Inputs.Count.ShouldBe(BrainPorts.Of(creature).Inputs.Count);
    }

    [Fact]
    public void For_AnUnnamedPiston_NamesItsTwoInputsAndTwoOutputsByItsDefaultName()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0))],
            [],
            [],
            [new PistonDef(3, 1, 2)]);

        var labels = BrainPortLabels.For(creature);

        var piston = UiText.Format("Piston {0}", 1);
        labels.Inputs.ShouldBe([UiText.Format("{0}:\u00A0length", piston), UiText.Format("{0}:\u00A0speed", piston)]);
        labels.Outputs.ShouldBe([UiText.Format("{0}:\u00A0length", piston), UiText.Format("{0}:\u00A0strength", piston)]);
    }

    [Fact]
    public void For_EverySensorKind_LabelsEachOfItsChannels()
    {
        foreach (var kind in Enum.GetValues<SensorKind>())
        {
            var creature = new CreatureDef(
                [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0))],
                [new BeamDef(3, 1, 2)],
                [new SensorDef(4, 3, kind)],
                []);

            BrainPortLabels.For(creature).Inputs.Count.ShouldBe(BrainPorts.SensorPorts(creature.Sensors[0]).Count());
        }
    }
}
