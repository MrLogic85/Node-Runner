namespace NodeRunner.Domain.Tests;

public sealed class Vector2DTests
{
    [Fact]
    public void Constructor_WithNonFiniteCoordinate_Throws()
    {
        Action xAction = () => _ = new Vector2D(double.NaN, 0);
        Action yAction = () => _ = new Vector2D(0, double.PositiveInfinity);

        xAction.ShouldThrow<ArgumentOutOfRangeException>();
        yAction.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
