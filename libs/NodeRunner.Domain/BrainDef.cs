using System.Collections.ObjectModel;

namespace NodeRunner.Domain;

/// <summary>
/// A saved brain as a graph (#536, #522): neurons and the connection genes between them. 0.13 only
/// builds direct brains, input ports straight to output ports, but the format already holds hidden
/// neurons, disabled genes and any connection pattern, so later brains need no new format.
/// <see cref="NextNeuronId"/> only grows, so a neuron id is never reused. See docs/SAVE_FORMAT.md.
/// </summary>
public sealed record BrainDef
{
    private readonly ReadOnlyCollection<NeuronDef> _neurons;
    private readonly ReadOnlyCollection<ConnectionGeneDef> _connections;

    public BrainDef(IReadOnlyList<NeuronDef> neurons, IReadOnlyList<ConnectionGeneDef> connections, int nextNeuronId)
    {
        ArgumentNullException.ThrowIfNull(neurons);
        ArgumentNullException.ThrowIfNull(connections);
        if (neurons.Contains(null!) || connections.Contains(null!))
        {
            throw new ArgumentException("A brain's lists cannot contain null.");
        }

        var byId = new Dictionary<int, NeuronDef>();
        var ports = new HashSet<(NeuronKind, int?, string?)>();
        foreach (var neuron in neurons)
        {
            if (!byId.TryAdd(neuron.Id, neuron))
            {
                throw new ArgumentException($"Neuron id {neuron.Id} is used more than once.");
            }

            if (neuron.Id >= nextNeuronId)
            {
                throw new ArgumentOutOfRangeException(nameof(nextNeuronId), "Every neuron id must be less than the next neuron id.");
            }

            if (neuron.Kind != NeuronKind.Hidden && !ports.Add((neuron.Kind, neuron.PartId, neuron.Channel)))
            {
                throw new ArgumentException($"Port {neuron.PartId}/{neuron.Channel} has more than one neuron.");
            }
        }

        var keys = new HashSet<(int, int)>();
        foreach (var gene in connections)
        {
            if (!byId.TryGetValue(gene.From, out var from) || !byId.TryGetValue(gene.To, out var to))
            {
                throw new ArgumentException($"Connection {gene.From}→{gene.To} refers to a missing neuron.");
            }

            if (from.Kind == NeuronKind.Output || to.Kind == NeuronKind.Input || from.Layer >= to.Layer)
            {
                throw new ArgumentException($"Connection {gene.From}→{gene.To} must run forward, from a lower layer to a higher one.");
            }

            if (!keys.Add((gene.From, gene.To)))
            {
                throw new ArgumentException($"Connection {gene.From}→{gene.To} is stored more than once.");
            }
        }

        if (nextNeuronId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nextNeuronId), "Next neuron id must be positive.");
        }

        _neurons = Array.AsReadOnly(neurons.ToArray());
        _connections = Array.AsReadOnly(connections.ToArray());
        NextNeuronId = nextNeuronId;
    }

    public IReadOnlyList<NeuronDef> Neurons => _neurons;

    public IReadOnlyList<ConnectionGeneDef> Connections => _connections;

    public int NextNeuronId { get; }
}
