namespace NodeRunner.Domain;

/// <summary>
/// One neuron in a saved brain (#536). A port neuron (input or output) stands for the
/// <see cref="BrainPort"/> with its <see cref="PartId"/> and <see cref="Channel"/>; a hidden one has
/// neither. <see cref="Layer"/> is 0 for inputs and grows towards the outputs, so every connection
/// runs from a lower layer to a higher one. An input neuron has no bias and passes its value through.
/// See <see cref="BrainDef"/> and docs/SAVE_FORMAT.md.
/// </summary>
public sealed record NeuronDef
{
    public NeuronDef(int id, NeuronKind kind, int? partId, string? channel, int layer, double bias, NeuronActivation activation)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Neuron id must be positive.");
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Neuron kind must be defined.");
        }

        if (!Enum.IsDefined(activation))
        {
            throw new ArgumentOutOfRangeException(nameof(activation), "Neuron activation must be defined.");
        }

        if (kind == NeuronKind.Hidden ? partId is not null || channel is not null : partId is not > 0 || string.IsNullOrWhiteSpace(channel))
        {
            throw new ArgumentException("A port neuron needs a part id and a channel; a hidden neuron has neither.");
        }

        if (kind == NeuronKind.Input ? layer != 0 || bias != 0 || activation != NeuronActivation.Identity : layer < 1)
        {
            throw new ArgumentException("An input neuron sits in layer 0 with no bias and passes its value through; other neurons sit in layer 1 or later.");
        }

        if (!double.IsFinite(bias))
        {
            throw new ArgumentOutOfRangeException(nameof(bias), "Bias must be finite.");
        }

        Id = id;
        Kind = kind;
        PartId = partId;
        Channel = channel;
        Layer = layer;
        Bias = bias;
        Activation = activation;
    }

    public int Id { get; }

    public NeuronKind Kind { get; }

    public int? PartId { get; }

    public string? Channel { get; }

    public int Layer { get; }

    public double Bias { get; }

    public NeuronActivation Activation { get; }
}
