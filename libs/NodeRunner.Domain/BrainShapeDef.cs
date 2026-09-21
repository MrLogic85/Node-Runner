namespace NodeRunner.Domain;

/// <summary>Saved hidden-network shape chosen before a Creation is saved.</summary>
public sealed record BrainShapeDef
{
    public const int MinimumHiddenLayers = 1;
    public const int MaximumHiddenLayers = 3;
    public const int MinimumNeuronsPerLayer = 1;
    public const int MaximumNeuronsPerLayer = 100;
    public const int DefaultHiddenLayers = 1;
    public const int DefaultNeuronsPerLayer = 4;

    public BrainShapeDef(int hiddenLayers, int neuronsPerLayer)
    {
        if (hiddenLayers is < MinimumHiddenLayers or > MaximumHiddenLayers)
        {
            throw new ArgumentOutOfRangeException(nameof(hiddenLayers));
        }

        if (neuronsPerLayer is < MinimumNeuronsPerLayer or > MaximumNeuronsPerLayer)
        {
            throw new ArgumentOutOfRangeException(nameof(neuronsPerLayer));
        }

        HiddenLayers = hiddenLayers;
        NeuronsPerLayer = neuronsPerLayer;
    }

    public int HiddenLayers { get; }

    public int NeuronsPerLayer { get; }

    public static BrainShapeDef Default { get; } = new(DefaultHiddenLayers, DefaultNeuronsPerLayer);

    public int[] ToLayerSizes(int inputCount, int outputCount)
    {
        if (inputCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(inputCount));
        }

        if (outputCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(outputCount));
        }

        var layers = new int[HiddenLayers + 2];
        layers[0] = inputCount;
        for (var i = 0; i < HiddenLayers; i++)
        {
            layers[i + 1] = NeuronsPerLayer;
        }

        layers[^1] = outputCount;
        return layers;
    }
}
