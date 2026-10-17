namespace NodeRunner.Domain.Tests;

public sealed class SelectionMarksTests
{
    [Fact]
    public void JointHalo_SitsTheGapOutsideTheJoint_AndALineEndsWhereItMeetsTheCircle()
    {
        SelectionMarks.JointHalo(15).ShouldBe(18);
        foreach (var (reach, offset) in new[] { (15.0, 10.0), (18, 6), (18, -6) })
        {
            var along = SelectionMarks.LineEnd(reach, offset);
            Math.Sqrt((along * along) + (offset * offset)).ShouldBe(reach, 1e-9);
        }

        (SelectionMarks.LineEnd(15, 15), SelectionMarks.LineEnd(15, 20)).ShouldBe((0, 0));
    }

    // An Angle sensor's arc lies an arc gap outside its joint's outermost part; its halo a gap outside the arc.
    [Fact]
    public void AngleSensorArc_ClearsTheJointsRubberAndTouchSensor()
    {
        foreach (var (rubber, touch, radius) in new[] { (false, false, 23.0), (true, false, 26), (false, true, 28), (true, true, 28) })
        {
            AngleSensorDef.ArcRadius(rubber, touch).ShouldBe(radius, $"rubber {rubber}, touch {touch}");
        }

        new[] { 2, 3, 4 }.Select(node => AngleSensorDef.ArcRadiusAt(node, rubberNodes: [1, 2], touchSensorNodes: [3])).ShouldBe([26, 28, 23]);
        (AngleSensorDef.MarkRadius(23), AngleSensorDef.HaloRadius(23), AngleSensorDef.HaloRadius(28)).ShouldBe((25, 28, 33));
    }
}
