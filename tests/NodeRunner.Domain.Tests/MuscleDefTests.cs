namespace NodeRunner.Domain.Tests;

public sealed class MuscleDefTests
{
    [Fact]
    public void Constructor_WithNonPositiveRestLength_Throws()
    {
        var action = () => new MuscleDef(0, 1, 0, 10);

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNonPositiveMaxForce_Throws()
    {
        var action = () => new MuscleDef(0, 1, 2, 0);

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNonFiniteValues_Throws()
    {
        var restLengthAction = () => new MuscleDef(0, 1, double.PositiveInfinity, 10);
        var maxForceAction = () => new MuscleDef(0, 1, 2, double.NaN);

        restLengthAction.ShouldThrow<ArgumentOutOfRangeException>();
        maxForceAction.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
