namespace NodeRunner.Mechanics.Tests;

public sealed class TouchTests
{
    // A joint of radius 10 has its 5-unit cushion from 10 to 15 out.
    [Fact]
    public void Reading_RisesAcrossTheCushion_FromItsOuterEdgeToTheJoint()
    {
        foreach (var (groundDistance, reading) in new[] { (20.0, 0.0), (15, 0), (12.5, 0.5), (10, 1), (8, 1) })
        {
            Touch.Reading(groundDistance, jointRadius: 10).ShouldBe(reading);
        }
    }
}
