namespace NodeRunner.Domain.Tests;

public sealed class BrainShapeDefTests
{
    [Fact]
    public void Constructor_WithValidShape_SetsProperties()
    {
        var shape = new BrainShapeDef(2, 12);

        shape.HiddenLayers.ShouldBe(2);
        shape.NeuronsPerLayer.ShouldBe(12);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(4, 4)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Constructor_WithOutOfRangeValues_Throws(int layers, int neurons)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new BrainShapeDef(layers, neurons));
    }

    [Fact]
    public void ToLayerSizes_RepeatsHiddenLayerShape()
    {
        var shape = new BrainShapeDef(3, 9);

        shape.ToLayerSizes(6, 2).ShouldBe([6, 9, 9, 9, 2]);
    }
}
