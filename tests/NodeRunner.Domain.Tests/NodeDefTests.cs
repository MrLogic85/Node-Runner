namespace NodeRunner.Domain.Tests;

public sealed class NodeDefTests
{
    [Fact]
    public void Constructor_WithValidRadius_StoresValues()
    {
        var node = new NodeDef(new Vector2D(1, 2), 3);

        node.Position.ShouldBe(new Vector2D(1, 2));
        node.Radius.ShouldBe(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_WithInvalidRadius_Throws(double radius)
    {
        var action = () => new NodeDef(new Vector2D(0, 0), radius);

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
