namespace NodeRunner.Domain.Tests;

public sealed class BoneDefTests
{
    [Fact]
    public void Constructor_WithSameJoint_Throws()
    {
        var action = () => new BoneDef(1, 1);

        action.ShouldThrow<ArgumentException>();
    }
}
