namespace NodeRunner.Mechanics;

/// <summary>
/// The update of a <see cref="Domain.MemoryCellDef"/>'s number <c>m</c>: a one-unit GRU without a reset gate.
/// Each tick the network runs with the current <c>m</c> as the cell's value input, then <see cref="Next"/>
/// moves <c>m</c> the write output's share of the way to the target output, and the network reads the new
/// <c>m</c> on the next tick. <c>m</c> is <see cref="Start"/> at every trial start and is never saved. The loop
/// runs outside the network, so the network stays feed-forward. See docs/CREATURE_MODEL.md → "Memory cell".
/// </summary>
public static class MemoryCell
{
    /// <summary>What <c>m</c> holds at every trial start.</summary>
    public const double Start = 0;

    /// <summary>
    /// <c>m</c> after this tick: <c>m + write · (target − m)</c>. With target and <paramref name="m"/> in −1…1 and
    /// <paramref name="write"/> in 0…1, as the outputs' activations give, it stays in −1…1.
    /// </summary>
    /// <param name="m">What the cell holds, which the network read this tick.</param>
    /// <param name="target">The target output, −1…1: what to remember.</param>
    /// <param name="write">The write output, 0…1: 0 keeps <paramref name="m"/>, 1 takes the target.</param>
    public static double Next(double m, double target, double write) => m + (write * (target - m));
}
