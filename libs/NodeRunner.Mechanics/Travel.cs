namespace NodeRunner.Mechanics;

/// <summary>
/// The travel a Piston or Spring moves in (#870, #835): from its shortest length to its longest,
/// which is its shortest grown by its stroke, with the drawn length <c>at</c> of the way along.
/// </summary>
internal static class Travel
{
    /// <param name="drawnLength">The distance between its nodes in the drawing.</param>
    /// <param name="stroke">How much it grows from its shortest length, as a share of it.</param>
    /// <param name="at">Where the drawn length sits: 0 at its shortest, 1 at its longest; outside 0…1 it lies beyond a stop.</param>
    public static double Shortest(double drawnLength, double stroke, double at) => drawnLength / (1 + (at * stroke));

    public static double Longest(double drawnLength, double stroke, double at) => Shortest(drawnLength, stroke, at) * (1 + stroke);
}
