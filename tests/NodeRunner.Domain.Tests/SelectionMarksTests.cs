namespace NodeRunner.Domain.Tests;

public sealed class SelectionMarksTests
{
    [Fact]
    public void JointHalo_SitsTheGapOutsideTheJoint()
    {
        SelectionMarks.JointHalo(15).ShouldBe(15 + SelectionMarks.Gap);
    }

    [Theory]
    [InlineData(15, 10)]
    [InlineData(18, 6)]
    [InlineData(18, -6)]
    public void LineEnd_IsWhereTheLineMeetsTheCircle(double reach, double offset)
    {
        var along = SelectionMarks.LineEnd(reach, offset);

        Math.Sqrt((along * along) + (offset * offset)).ShouldBe(reach, 1e-9);
    }

    [Theory]
    [InlineData(15, 15)]
    [InlineData(15, 20)]
    public void LineEnd_OutsideTheCircle_IsTheJointsCentre(double reach, double offset)
    {
        SelectionMarks.LineEnd(reach, offset).ShouldBe(0);
    }
}
