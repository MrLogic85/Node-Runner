namespace NodeRunner.Domain.Tests;

public sealed class JointDefTests
{
    [Fact]
    public void Constructor_WithNonPositiveRadius_Throws()
    {
        var action = () => new JointDef(new Vector2D(0, 0), 0);

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNonFiniteRadius_Throws()
    {
        var action = () => new JointDef(new Vector2D(0, 0), double.NaN);

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
