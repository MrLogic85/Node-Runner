namespace NodeRunner.Mechanics.Tests;

public sealed class TouchTests
{
    // A joint of radius 10 has its 5-unit cushion from 10 to 15 out.
    [Theory]
    [InlineData(20, 0)]
    [InlineData(15, 0)]
    [InlineData(12.5, 0.5)]
    [InlineData(10, 1)]
    [InlineData(8, 1)]
    public void Reading_RisesAcrossTheCushion_FromItsOuterEdgeToTheJoint(double groundDistance, double reading)
    {
        Touch.Reading(groundDistance, jointRadius: 10).ShouldBe(reading);
    }
}
