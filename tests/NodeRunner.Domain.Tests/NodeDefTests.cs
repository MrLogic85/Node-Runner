namespace NodeRunner.Domain.Tests;

public sealed class NodeDefTests
{
    [Fact]
    public void PlainJointRadius_Is15()
    {
        NodeDef.PlainJointRadius.ShouldBe(15);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithInvalidId_Throws(int id)
    {
        var action = () => new NodeDef(id, new Vector2D(0, 0));

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
