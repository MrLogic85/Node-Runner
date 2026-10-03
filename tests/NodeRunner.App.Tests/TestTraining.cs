using NodeRunner.Domain;
using NodeRunner.ML.Brains;

namespace NodeRunner.App.Tests;

internal static class TestTraining
{
    public static TrainingRunDef Run { get; } = new(1, 1, 0, MapIds.Flat);

    /// <summary>A direct brain with one sense, one output and one connection.</summary>
    public static BrainDef Brain { get; } = new(
        [
            new NeuronDef(1, NeuronKind.Input, 3, "along", 0, 0, NeuronActivation.Identity),
            new NeuronDef(2, NeuronKind.Output, 1, "target:2", 1, 0.1, NeuronActivation.Tanh),
        ],
        [new ConnectionGeneDef(1, 2, 0.5, true)],
        nextNeuronId: 3);

    /// <summary>A training at <paramref name="generation"/> whose best ever, <paramref name="bestDistance"/>, came from that generation.</summary>
    public static TrainingStateDef State(int generation, double bestDistance = 1, TrainingRunDef? latest = null) =>
        new(Brain, generation, latest ?? Run, new TrainingBestDef(generation, bestDistance, MapIds.Flat));

    /// <summary>
    /// <see cref="State"/> with a direct brain for <paramref name="creature"/>'s own ports, every gene
    /// and bias a distinct value, so a test can tell whether each one was kept.
    /// </summary>
    public static TrainingStateDef StateFor(CreatureDef creature, int generation, double bestDistance = 1, TrainingRunDef? latest = null)
    {
        var ports = BrainPorts.Of(creature);
        var genome = Enumerable.Range(1, (ports.Inputs.Count * ports.Outputs.Count) + ports.Outputs.Count).Select(i => i / 10.0).ToArray();
        return new(DirectBrain.ToBrainDef(ports, genome, previous: null), generation, latest ?? Run, new TrainingBestDef(generation, bestDistance, MapIds.Flat));
    }
}
