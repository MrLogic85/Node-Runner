using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BrainSetupPresentationTests
{
    [Fact]
    public void For_TheReferenceDefaults_MatchesItsPicture()
    {
        var setup = BrainSetupPresentation.For(new BrainShapeDef(1, 4), inputCount: 3, outputCount: 4);

        setup.RecommendedNeurons.ShouldBe(4);
        setup.DefaultText.ShouldBe("default 4");
        setup.Columns.Select(column => column.Count).ShouldBe([3, 4, 4]);
        setup.Connections.ShouldBe(28);
        setup.LayersLabel.ShouldBe("Layer 1");
    }

    [Fact]
    public void For_ThreeLayersFromTheReference_CountsEveryConnection()
    {
        var setup = BrainSetupPresentation.For(new BrainShapeDef(3, 4), inputCount: 3, outputCount: 4);

        setup.Connections.ShouldBe((3 * 4) + (4 * 4) + (4 * 4) + (4 * 4));
        setup.LayersLabel.ShouldBe("Layers 1–3");
    }

    [Theory]
    [InlineData(1, true, "Recommended for simple tasks.")]
    [InlineData(2, true, "Recommended for harder navigation.")]
    [InlineData(3, false, "Not recommended: slow to learn. For experiments.")]
    public void For_EachLayerCount_SaysWhetherItIsRecommended(int layers, bool recommended, string advice)
    {
        var setup = BrainSetupPresentation.For(new BrainShapeDef(layers, 4), inputCount: 3, outputCount: 4);

        setup.IsRecommended.ShouldBe(recommended);
        setup.Advice.ShouldBe(advice);
    }

    [Fact]
    public void For_ALayerOfMoreThanSix_DrawsSixAndCaptionsTheRest()
    {
        var setup = BrainSetupPresentation.For(new BrainShapeDef(1, 24), inputCount: 3, outputCount: 4);

        setup.Columns[1].ShouldBe(new BrainSetupColumn(24, 6, "+18"));
        setup.Columns[0].ShouldBe(new BrainSetupColumn(3, 3, string.Empty));
    }

    [Fact]
    public void For_NoAnatomy_HasNoColumnsOrConnections()
    {
        var setup = BrainSetupPresentation.For(BrainShapeDef.Default, inputCount: 0, outputCount: 0);

        setup.HasAnatomy.ShouldBeFalse();
        setup.Columns.ShouldBeEmpty();
        setup.Connections.ShouldBe(0);
        setup.RecommendedNeurons.ShouldBe(BrainShapeDef.MinimumNeuronsPerLayer);
    }

    // Sensors without motor beams give inputs but no outputs, the state Build shows while drawing.
    [Theory]
    [InlineData(6, 0)]
    [InlineData(0, 3)]
    public void For_SensesOrOutputsMissing_HasNoColumnsOrConnections(int inputCount, int outputCount)
    {
        var setup = BrainSetupPresentation.For(BrainShapeDef.Default, inputCount, outputCount);

        setup.HasAnatomy.ShouldBeFalse();
        setup.Columns.ShouldBeEmpty();
        setup.Connections.ShouldBe(0);
    }

    [Theory]
    [InlineData(3, 4, 4)]
    [InlineData(3, 2, 3)]
    [InlineData(300, 0, BrainShapeDef.MaximumNeuronsPerLayer)]
    public void RecommendedNeuronsFor_IsHalfTheSensesAndOutputsRoundedUp(int inputs, int outputs, int expected) =>
        BrainSetupPresentation.RecommendedNeuronsFor(inputs, outputs).ShouldBe(expected);

    [Fact]
    public void Positions_PlaceTheThumbAndTheDefaultMarkerOnTheNeuronRange()
    {
        var setup = BrainSetupPresentation.For(new BrainShapeDef(1, 100), inputCount: 1, outputCount: 1);

        setup.NeuronsPosition.ShouldBe(1);
        setup.DefaultPosition.ShouldBe(0);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 100)]
    [InlineData(0.5, 51)]
    [InlineData(-1, 1)]
    public void WithNeuronsAt_MapsASliderPositionOntoTheNeuronRange(double position, int neurons)
    {
        var setup = BrainSetupPresentation.For(new BrainShapeDef(2, 4), inputCount: 3, outputCount: 4);

        setup.WithNeuronsAt(position).ShouldBe(new BrainShapeDef(2, neurons));
    }

    [Theory]
    [InlineData(1, -1, 1)]
    [InlineData(100, 1, 100)]
    [InlineData(4, 1, 5)]
    public void WithNeuronsStepped_StaysInTheAllowedRange(int neurons, int delta, int expected)
    {
        var setup = BrainSetupPresentation.For(new BrainShapeDef(1, neurons), inputCount: 3, outputCount: 4);

        setup.WithNeuronsStepped(delta).ShouldBe(new BrainShapeDef(1, expected));
    }

    [Fact]
    public void WithHiddenLayers_KeepsTheNeurons()
    {
        var setup = BrainSetupPresentation.For(new BrainShapeDef(1, 9), inputCount: 3, outputCount: 4);

        setup.WithHiddenLayers(3).ShouldBe(new BrainShapeDef(3, 9));
    }

    [Fact]
    public void Defaults_IsOneLayerWithTheDefaultNeurons()
    {
        var setup = BrainSetupPresentation.For(new BrainShapeDef(3, 50), inputCount: 3, outputCount: 4);

        setup.Defaults.ShouldBe(new BrainShapeDef(1, 4));
    }
}
