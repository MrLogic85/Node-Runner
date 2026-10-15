namespace NodeRunner.Domain;

/// <summary>A creature's network ports in runtime order: input <c>i</c> is the network's input <c>i</c>, output <c>j</c> its output <c>j</c>.</summary>
public sealed record NetworkPortLayout(IReadOnlyList<NetworkPort> Inputs, IReadOnlyList<NetworkPort> Outputs)
{
    public static NetworkPortLayout Empty { get; } = new([], []);
}
