namespace NodeRunner.Domain.Tests;

public sealed class BeamDefTests
{
    [Fact]
    public void Constructor_WithSameNodeTwice_Throws()
    {
        var action = () => new BeamDef(3, 1, 1);

        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNegativeNodeIndex_Throws()
    {
        var action = () => new BeamDef(3, -1, 1);

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
