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

        var accelerometer = UiText.Format("Accel {0}", 1);
        var eye = UiText.AsWritten("Eye");
        var ram = UiText.AsWritten("Ram");
        labels.Inputs.ShouldBe(
        [
            UiText.Format("{0}:\u00A0along", accelerometer),
            UiText.Format("{0}:\u00A0across", accelerometer),
            UiText.Format("{0}:\u00A0left", eye),
            UiText.Format("{0}:\u00A0centre", eye),
            UiText.Format("{0}:\u00A0right", eye),
            UiText.Format("{0}:\u00A0hit", eye),
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
    public void For_AnUnnamedServo_NamesItsInputsAndOutputsByTheQuantitiesTheySet()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0)), new NodeDef(3, new Vector2D(0, 2))],
            [new BeamDef(4, 1, 2), new BeamDef(5, 1, 3)],
            [],
            [new ServoDef(6, 1, 4, 5)],
            [],
            [],
            nextPartId: 7);

        var labels = BrainPortLabels.For(creature);

        var servo = UiText.Format("Servo {0}", 1);
        labels.Inputs.ShouldBe([UiText.Format("{0}:\u00A0angle", servo), UiText.Format("{0}:\u00A0speed", servo)]);
        labels.Outputs.ShouldBe([UiText.Format("{0}:\u00A0angle", servo), UiText.Format("{0}:\u00A0strength", servo)]);
    }

    [Theory]
    [InlineData(1, new[] { "centre", "hit" })]
    [InlineData(3, new[] { "left", "centre", "right", "hit" })]
    [InlineData(5, new[] { "far left", "left", "centre", "right", "far right", "hit" })]
    public void For_ACamera_NamesItsRaysFromLeftToRight_ThenHit(int rays, string[] readings)
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0))],
            [new BeamDef(3, 1, 2)],
            [new SensorDef(4, 3, SensorKind.Camera, "Eye", rays: rays)],
            []);

        var eye = UiText.AsWritten("Eye");
        BrainPortLabels.For(creature).Inputs.ShouldBe(readings.Select(reading => UiText.Format($"{{0}}:\u00A0{reading}", eye)));
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
