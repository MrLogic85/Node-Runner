namespace NodeRunner.Mechanics;

/// <summary>
/// The travel a Piston or Spring moves in (#870, #835). It is measured on the gap between its two
/// joints' edges, where the movement happens: its shortest gap grown by its stroke is its longest,
/// with the drawn gap <c>at</c> of the way along. A joint with a Servo is bigger, so its gap is shorter.
/// </summary>
internal static class Travel
{
    /// <param name="drawnLength">The distance between its nodes' centres in the drawing.</param>
    /// <param name="jointRadii">Its two joints' radii together; a gap that is not there counts as none.</param>
    /// <param name="stroke">How much its gap grows from its shortest, as a share of it.</param>
    /// <param name="at">Where the drawn gap sits: 0 at its shortest, 1 at its longest.</param>
    public static double Shortest(double drawnLength, double jointRadii, double stroke, double at) =>
        drawnLength - Gap(drawnLength, jointRadii) + (Gap(drawnLength, jointRadii) / (1 + (at * stroke)));

    public static double Longest(double drawnLength, double jointRadii, double stroke, double at) =>
        drawnLength - Gap(drawnLength, jointRadii) + (Gap(drawnLength, jointRadii) * (1 + stroke) / (1 + (at * stroke)));

    /// <summary>The drawn gap between its joints' edges.</summary>
    public static double Gap(double drawnLength, double jointRadii) => Math.Max(drawnLength - jointRadii, 0);
}
