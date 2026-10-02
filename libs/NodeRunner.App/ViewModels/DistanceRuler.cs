using System.Globalization;

namespace NodeRunner.App.ViewModels;

/// <summary>One tick of the Training ruler: where it is in world units, and its label if it has one.</summary>
/// <param name="X">The tick's position along the ground, in world units.</param>
/// <param name="IsMajor">A whole metre: a long tick with a label.</param>
/// <param name="Label">The distance from the start, such as "3 m"; empty on a minor tick.</param>
public readonly record struct RulerMark(double X, bool IsMajor, string Label);

/// <summary>
/// The distance marks along the Training ground (#668): a labelled tick every metre and a minor one
/// halfway, counted from the trial's start (0 m), so a moving creature shows its progress against
/// them. Behind the start they count down ("-1 m").
/// </summary>
public static class DistanceRuler
{
    /// <summary>The gap between ticks, half a metre, in world units.</summary>
    public const double TickSpacing = Metres.WorldUnitsPerMetre / _ticksPerMetre;

    /// <summary>
    /// The most ticks one call lists. A view never spans this far; the cap only keeps a corrupt
    /// range from listing millions.
    /// </summary>
    public const int MaxMarks = 1000;

    private const int _ticksPerMetre = 2;

    /// <summary>The farthest tick listed, where a double still counts whole ticks exactly (2^53).</summary>
    private const double _maxTick = 9_007_199_254_740_992;

    /// <summary>Fills <paramref name="marks"/> with every tick from <paramref name="left"/> to <paramref name="right"/>, in order.</summary>
    /// <param name="startX">Where 0 m is, in world units.</param>
    /// <param name="left">The left end of the range, in world units.</param>
    /// <param name="right">The right end of the range, in world units.</param>
    /// <param name="marks">Cleared, then filled, so a caller can reuse one list every frame.</param>
    public static void Fill(double startX, double left, double right, List<RulerMark> marks)
    {
        ArgumentNullException.ThrowIfNull(marks);
        marks.Clear();
        if (!double.IsFinite(startX) || !double.IsFinite(left) || !double.IsFinite(right) || right < left)
        {
            return;
        }

        // Checked as doubles: a cast to long clamps a huge tick, which would slip past the cap.
        var first = Math.Ceiling((left - startX) / TickSpacing);
        var last = Math.Floor((right - startX) / TickSpacing);
        if (!(last - first < MaxMarks) || Math.Max(Math.Abs(first), Math.Abs(last)) > _maxTick)
        {
            return;
        }

        for (var tick = (long)first; tick <= (long)last; tick++)
        {
            var isMajor = tick % _ticksPerMetre == 0;
            var label = isMajor
                ? string.Create(CultureInfo.InvariantCulture, $"{tick / _ticksPerMetre} m")
                : string.Empty;
            marks.Add(new RulerMark(startX + (tick * TickSpacing), isMajor, label));
        }
    }
}
