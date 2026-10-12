namespace NodeRunner.Mechanics;

/// <summary>
/// How hard a Piston's or Spring's end stop holds before it slides (#1115,
/// <c>docs/CREATURE_MODEL.md</c> → Piston).
/// </summary>
public static class EndStop
{
    /// <summary>How many times the creature's own most force a stop holds, room for impacts and leverage.</summary>
    public const double Margin = 3;

    /// <summary>
    /// The most force a stop holds: <see cref="Margin"/> times what the creature can put on it by
    /// itself, every Piston pushing at its strength plus its weight. A Spring adds nothing, since its
    /// rest length stays inside its stops.
    /// </summary>
    public static double MaxForce(IEnumerable<double> pistonStrengths, double mass, double gravity)
    {
        ArgumentNullException.ThrowIfNull(pistonStrengths);
        return Margin * (pistonStrengths.Sum() + (mass * gravity));
    }
}
