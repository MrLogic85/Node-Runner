namespace NodeRunner.Domain.Tests;

public sealed class BeamDefTests
{
    [Fact]
    public void Constructor_WithDistinctNodes_StoresValues()
    {
        var beam = new BeamDef(0, 1);

        beam.NodeA.ShouldBe(0);
        beam.NodeB.ShouldBe(1);
    }

    [Fact]
    public void Constructor_WithSameNodeTwice_Throws()
    {
        var action = () => new BeamDef(0, 0);

        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNegativeNodeIndex_Throws()
    {
        var action = () => new BeamDef(-1, 0);

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
