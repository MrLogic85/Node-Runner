namespace NodeRunner.Domain.Tests;

/// <summary>Which parts a <see cref="RaisedParts"/> draws on the selected surface, each kind by its own rule.</summary>
public sealed class RaisedPartsTests
{
    // Joint 1 has its Wheel face raised but not its ring; joint 2 is raised; beam 6 is raised, with the Camera on it.
    private static readonly RaisedParts _raised = new(
        Links: new HashSet<int> { 6 }, Joints: new HashSet<int> { 2 }, Sensors: new HashSet<int>(), Wheels: new HashSet<int> { 1 });

    [Fact]
    public void Contains_RaisesEachKindWithWhatItDrawsWith()
    {
        EveryPartKind.HoldsEveryKind.ShouldBeTrue();
        EveryPartKind.Creature.Parts.Where(_raised.Contains).Select(part => part.Id).Order().ShouldBe([2, 6, 8, 12, 15]);
    }

    // A Wheel's face rises with Wheels; every other slot with its joint.
    [Fact]
    public void JointPart_TakesTheWheelFaceApart()
    {
        (_raised.JointPart(1, JointSlot.Wheel), _raised.JointPart(1, JointSlot.HingePart), _raised.JointPart(1, JointSlot.TouchSensor))
            .ShouldBe((true, false, false));
        (_raised.JointPart(2, JointSlot.Wheel), _raised.JointPart(2, JointSlot.Rubber)).ShouldBe((false, true));
    }

    // A sensor rises when selected itself, or with its beam.
    [Fact]
    public void Contains_RaisesASelectedSensorOrOneOnARaisedBeam()
    {
        var selectedAccelerometer = _raised with { Sensors = new HashSet<int> { 7 } };

        selectedAccelerometer.Contains(EveryPartKind.Creature.FindPart(7)!).ShouldBeTrue();
        selectedAccelerometer.Contains(EveryPartKind.Creature.FindPart(8)!).ShouldBeTrue();
        _raised.Contains(EveryPartKind.Creature.FindPart(7)!).ShouldBeFalse();
    }
}
