using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>One column of the Brain setup picture: how many neurons the layer has.</summary>
/// <param name="Count">The layer's neurons, shown under the column.</param>
/// <param name="Shown">How many are drawn; a large layer draws only the first few.</param>
/// <param name="MoreText">"+N" for the neurons not drawn, or empty when all are drawn.</param>
public sealed record BrainSetupColumn(int Count, int Shown, string MoreText);

/// <summary>
/// The Brain setup page (<c>reference design/components/BrainSetup</c>): hidden layers, the neurons
/// every hidden layer shares, whether the choice is recommended, and the brain it makes. The default
/// neuron count is (senses + outputs) / 2 rounded up, so it follows the creature's anatomy.
/// </summary>
public sealed record BrainSetupPresentation(
    BrainShapeDef Shape,
    int RecommendedNeurons,
    bool IsRecommended,
    string Advice,
    string LayersLabel,
    double NeuronsPosition,
    double DefaultPosition,
    string DefaultText,
    bool HasAnatomy,
    IReadOnlyList<BrainSetupColumn> Columns,
    int Connections)
{
    /// <summary>A layer with more neurons than this draws this many and a "+N" caption.</summary>
    public const int MaxShownNeurons = 6;

    private const int _neuronRange = BrainShapeDef.MaximumNeuronsPerLayer - BrainShapeDef.MinimumNeuronsPerLayer;

    public static BrainSetupPresentation For(BrainShapeDef shape, int inputCount, int outputCount)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var recommended = RecommendedNeuronsFor(inputCount, outputCount);
        var hasAnatomy = inputCount > 0 && outputCount > 0;
        var sizes = hasAnatomy ? shape.ToLayerSizes(inputCount, outputCount) : [];
        return new BrainSetupPresentation(
            shape,
            recommended,
            IsRecommended: shape.HiddenLayers < BrainShapeDef.MaximumHiddenLayers,
            Advice: AdviceFor(shape.HiddenLayers),
            LayersLabel: shape.HiddenLayers == 1 ? "Layer 1" : $"Layers 1–{shape.HiddenLayers}",
            NeuronsPosition: PositionOf(shape.NeuronsPerLayer),
            DefaultPosition: PositionOf(recommended),
            DefaultText: $"default {recommended}",
            hasAnatomy,
            sizes.Select(ColumnFor).ToArray(),
            Connections: sizes.Zip(sizes.Skip(1), (from, to) => from * to).Sum());
    }

    /// <summary>The default neurons per layer: (senses + outputs) / 2 rounded up, within the allowed range.</summary>
    public static int RecommendedNeuronsFor(int inputCount, int outputCount) =>
        Math.Clamp(
            (int)Math.Ceiling((inputCount + outputCount) / 2.0),
            BrainShapeDef.MinimumNeuronsPerLayer,
            BrainShapeDef.MaximumNeuronsPerLayer);

    /// <summary>What Use defaults sets: one hidden layer with the default neurons.</summary>
    public BrainShapeDef Defaults => new(BrainShapeDef.DefaultHiddenLayers, RecommendedNeurons);

    public BrainShapeDef WithHiddenLayers(int hiddenLayers) => new(hiddenLayers, Shape.NeuronsPerLayer);

    /// <summary>The shape for a slider position from 0 (fewest neurons) to 1 (most).</summary>
    public BrainShapeDef WithNeuronsAt(double position) =>
        new(Shape.HiddenLayers, BrainShapeDef.MinimumNeuronsPerLayer + (int)Math.Round(Math.Clamp(position, 0, 1) * _neuronRange));

    /// <summary>The shape one stepper press away, kept within the allowed range.</summary>
    public BrainShapeDef WithNeuronsStepped(int delta) =>
        new(Shape.HiddenLayers, Math.Clamp(
            Shape.NeuronsPerLayer + delta,
            BrainShapeDef.MinimumNeuronsPerLayer,
            BrainShapeDef.MaximumNeuronsPerLayer));

    private static string AdviceFor(int hiddenLayers) => hiddenLayers switch
    {
        1 => "Recommended for simple tasks.",
        2 => "Recommended for harder navigation.",
        _ => "Not recommended: slow to learn. For experiments.",
    };

    private static double PositionOf(int neurons) =>
        (neurons - BrainShapeDef.MinimumNeuronsPerLayer) / (double)_neuronRange;

    private static BrainSetupColumn ColumnFor(int count)
    {
        var shown = Math.Min(count, MaxShownNeurons);
        return new BrainSetupColumn(count, shown, count > shown ? $"+{count - shown}" : string.Empty);
    }
}
