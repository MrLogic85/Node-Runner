namespace NodeRunner.Domain.Tests;

public sealed class CoreDefTests
{
    [Fact]
    public void Constructor_WithValidNodeIndex_StoresValue()
    {
        var core = new CoreDef(3, 2);

        core.Id.ShouldBe(3);
        core.NodeId.ShouldBe(2);
    }

    [Fact]
    public void Constructor_WithNegativeNodeIndex_Throws()
    {
        var action = () => new CoreDef(3, -1);

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
