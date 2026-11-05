using System.Diagnostics;

namespace NodeRunner.Mechanics;

/// <summary>
/// The update of a <see cref="Domain.MemoryCellDef"/>'s number <c>m</c>: a one-unit GRU without a reset gate.
/// Each tick the network runs with the current <c>m</c> as the cell's value input, then <see cref="Next"/>
/// moves <c>m</c> toward the target output at a rate the write output sets, and the network reads the new
/// <c>m</c> on the next tick. <c>m</c> is <see cref="Start"/> at every trial start and is never saved. The loop
/// runs outside the network, so the network stays feed-forward. See docs/CREATURE_MODEL.md → "Memory cell".
/// </summary>
public static class MemoryCell
{
    /// <summary>What <c>m</c> holds at every trial start.</summary>
    public const double Start = 0;

    /// <summary>The time constant of a full write, in seconds (docs/CREATURE_MODEL.md → "Memory cell").</summary>
    public const double WriteTime = 1;

    /// <summary>
    /// <c>m</c> after a tick of <paramref name="delta"/> seconds: <c>m + write · delta / WriteTime · (target − m)</c>.
    /// With target and <paramref name="m"/> in −1…1 and <paramref name="write"/> in 0…1, as the outputs' activations
    /// give, it stays in −1…1 and never overshoots. A step longer than <see cref="WriteTime"/> fails in a debug
    /// build; a release build takes the target.
    /// </summary>
    /// <param name="m">What the cell holds, which the network read this tick.</param>
    /// <param name="target">The target output, −1…1: what to remember.</param>
    /// <param name="write">The write output, 0…1: 0 keeps <paramref name="m"/>, 1 moves it at full rate.</param>
    /// <param name="delta">The tick's length in seconds, the fixed physics step.</param>
    public static double Next(double m, double target, double write, double delta)
    {
        Debug.Assert(delta is >= 0 and <= WriteTime, $"A Memory cell tick of {delta} s is outside 0…{WriteTime} s.");
        return m + (Math.Min(write * delta / WriteTime, 1) * (target - m));
    }
}
