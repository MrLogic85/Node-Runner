namespace NodeRunner.Domain.Tests;

public sealed class BeamDefTests
{
    [Fact]
    public void Constructor_WithDistinctNodes_StoresValues()
    {
        var beam = new BeamDef(3, 1, 2);

        beam.Id.ShouldBe(3);
        beam.NodeA.ShouldBe(1);
        beam.NodeB.ShouldBe(2);
    }

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
