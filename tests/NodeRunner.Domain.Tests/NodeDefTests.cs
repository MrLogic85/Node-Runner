namespace NodeRunner.Domain.Tests;

public sealed class NodeDefTests
{
    [Fact]
    public void Constructor_StoresValues()
    {
        var node = new NodeDef(1, new Vector2D(1, 2), "Knee");

        node.Id.ShouldBe(1);
        node.Position.ShouldBe(new Vector2D(1, 2));
        node.Name.ShouldBe("Knee");
    }

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
