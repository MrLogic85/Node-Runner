namespace NodeRunner.Domain;

/// <summary>A creature's brain ports in runtime order: input <c>i</c> is the brain's input <c>i</c>, output <c>j</c> its output <c>j</c>.</summary>
public sealed record BrainPortLayout(IReadOnlyList<BrainPort> Inputs, IReadOnlyList<BrainPort> Outputs)
{
    public static BrainPortLayout Empty { get; } = new([], []);
}
