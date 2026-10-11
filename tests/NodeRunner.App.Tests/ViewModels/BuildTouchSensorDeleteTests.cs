using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>
/// Delete and a joint's Touch sensor (#1043): the sensor has a brain port, so a locked Creation
/// keeps it, and it keeps its joint when the delete clears the joint's other parts.
/// </summary>
public sealed class BuildTouchSensorDeleteTests
{
    private const int _touchJoint = 2;
    private const int _wheelId = 6;
    private const int _touchId = 7;

    [Fact]
    public void LockedDelete_OfAJointWithATouchSensor_IsRefused_WithANoteOnTheJoint()
    {
        var build = Build(locked: true);
        build.SelectOnly(CreatureElementKind.Node, _touchJoint);

        build.DeleteLockedReason.ShouldNotBeNull();
        build.DeleteSelectedParts();

        build.CanvasNotes().ShouldBe([new CanvasNote(CanvasNoteKind.Danger, new CreatureElementSelection(CreatureElementKind.Node, _touchJoint), UiText.Plain("Locked: would change the model"))]);
        build.Snapshot().TouchSensors.ShouldHaveSingleItem();
    }

    [Fact]
    public void Delete_OfASelectedTouchSensor_RemovesIt_AndKeepsItsJoint()
    {
        var build = Build(locked: false);
        build.ReplaceSelection(PartSet.None with { TouchSensors = new HashSet<int> { _touchId } });

        build.DeleteSelectedParts();

        build.Snapshot().TouchSensors.ShouldBeEmpty();
        build.Snapshot().Nodes.ShouldContain(node => node.Id == _touchJoint);
    }

    [Fact]
    public void Delete_OfAWheelAndItsLinks_KeepsTheJoint_WhileItHoldsATouchSensor()
    {
        var build = Build(locked: false);
        build.ReplaceSelection(PartSet.None with { Beams = new HashSet<int> { 4, 5 }, Wheels = new HashSet<int> { _wheelId } });

        build.DeleteSelectedParts();

        build.Snapshot().Nodes.ShouldContain(node => node.Id == _touchJoint);
        build.Snapshot().TouchSensors.ShouldHaveSingleItem();
    }

    /// <summary>Joints 1, 2 and 3 in a row, beams 4 (1–2) and 5 (2–3); joint 2 holds Wheel 6 and Touch sensor 7.</summary>
    private static BuildViewModel Build(bool locked)
    {
        var build = new BuildViewModel();
        build.Load(
            new CreatureDef(
                [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(_touchJoint, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0))],
                [new BeamDef(4, 1, _touchJoint), new BeamDef(5, _touchJoint, 3)],
                [],
                [],
                [],
                [],
                [new WheelDef(_wheelId, _touchJoint)],
                [new TouchSensorDef(_touchId, _touchJoint)],
                nextPartId: 8),
            locked: locked);
        return build;
    }
}
