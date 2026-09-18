namespace NodeRunner.Domain.Tests;

public sealed class CoreDefTests
{
    [Fact]
    public void Constructor_WithValidNodeIndex_StoresValue()
    {
        var core = new CoreDef(2);

        core.NodeIndex.ShouldBe(2);
    }

    [Fact]
    public void Constructor_WithNegativeNodeIndex_Throws()
    {
        var action = () => new CoreDef(-1);

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
