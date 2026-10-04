namespace NodeRunner.Mechanics;

/// <summary>
/// How an output port's value becomes a physical demand (#535): a pose for a position output and a
/// force for a strength output. Which activation each output uses is the brain's contract
/// (<see cref="NodeRunner.Domain.PortSignals"/>). Stateless. See docs/CREATURE_MODEL.md.
/// </summary>
public static class OutputSignals
{
    /// <summary>
    /// The pose a position <paramref name="target"/> asks for, mapped piecewise so 0 is always the
    /// built pose even when it is off-centre: −1…0 spans <paramref name="min"/>…<paramref name="built"/>
    /// and 0…1 spans <paramref name="built"/>…<paramref name="max"/>. For a Piston, −1 is fully in and
    /// +1 fully out.
    /// </summary>
    public static double PositionFromTarget(double target, double min, double built, double max)
    {
        if (!(min <= built && built <= max))
        {
            throw new ArgumentOutOfRangeException(nameof(built), built, "The built pose must lie within min…max.");
        }

        var clamped = Math.Clamp(target, -1, 1);
        return clamped < 0
            ? built + (clamped * (built - min))
            : built + (clamped * (max - built));
    }

    /// <summary>The force a strength output asks for: its 0…1 share of the part's Strength setting.</summary>
    public static double StrengthFromOutput(double output, double strengthSetting) =>
        Math.Clamp(output, 0, 1) * strengthSetting;
}
