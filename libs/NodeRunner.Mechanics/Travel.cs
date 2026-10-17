using NodeRunner.Domain;

namespace NodeRunner.Mechanics;

/// <summary>
/// The travel a Piston or Spring moves in. It is measured on the gap between its two
/// hubs, where the movement happens: its shortest gap grown by its stroke is its longest,
/// with the drawn gap <c>at</c> of the way along. What sits on a joint does not change it.
/// </summary>
public static class Travel
{
    /// <summary>What every link's travel leaves out at its two ends: two hubs, whatever sits on its joints.</summary>
    public const double HubRadii = 2 * NodeDef.HubRadius;

    /// <param name="drawnLength">The distance between its nodes' centres in the drawing.</param>
    /// <param name="jointRadii">The radii its two ends leave out together, its hubs' (<see cref="HubRadii"/>); a gap that is not there counts as none.</param>
    /// <param name="stroke">How much its gap grows from its shortest, as a share of it.</param>
    /// <param name="at">Where the drawn gap sits: 0 at its shortest, 1 at its longest.</param>
    internal static double Shortest(double drawnLength, double jointRadii, double stroke, double at) =>
        drawnLength - Gap(drawnLength, jointRadii) + (Gap(drawnLength, jointRadii) / (1 + (at * stroke)));

    internal static double Longest(double drawnLength, double jointRadii, double stroke, double at) =>
        drawnLength - Gap(drawnLength, jointRadii) + (Gap(drawnLength, jointRadii) * (1 + stroke) / (1 + (at * stroke)));

    /// <summary>The drawn gap between its ends.</summary>
    internal static double Gap(double drawnLength, double jointRadii) => Math.Max(drawnLength - jointRadii, 0);
}
