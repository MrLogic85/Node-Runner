namespace NodeRunner.Domain.Tests;

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

    public static TrainingStateDef State(int generation, double bestFitness = 1, TrainingRunDef? run = null) =>
        new(Brain, generation, bestFitness, run ?? Run);
}
