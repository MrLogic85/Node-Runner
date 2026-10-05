namespace NodeRunner.App.ViewModels;

/// <summary>One tick of the Training ruler: where it is in world units, and its label if it has one.</summary>
/// <param name="X">The tick's position along the ground, in world units.</param>
/// <param name="IsMajor">A long tick: every power of ten that divides the label step, or under each label when the step is 5, 50, … m (#884).</param>
/// <param name="LabelMetre">
/// The metres from the start on every labelled metre, shown as <see cref="DistanceRuler.Label"/>;
/// otherwise null. A number, not text, which the ruler puts in the player's language as it draws (#756).
/// </param>
public readonly record struct RulerMark(double X, bool IsMajor, long? LabelMetre);

/// <summary>
/// The distance marks along the Training ground (#668): a long tick every metre and a minor one
/// halfway, counted from the trial's start (0 m), so a moving creature shows its progress against
/// them. Behind the start they count down ("-1 m"). Every metre is labelled, or every 2, 5, 10, …
/// when the camera zooms out so far that labels would overlap (#675). The ticks thin out with the
/// labels, so a far zoom does not crowd them (#884): a long tick every power of ten that divides
/// the label step, with a minor one halfway (every metre for 1 or 2 m labels, every 10 m for 10 or
/// 20 m), or, when the step is 5, 50, … m, a long tick under each label and a minor one every
/// 1, 10, … m between them.
/// </summary>
public static class DistanceRuler
{
    /// <summary>
    /// The most ticks one call lists. A view never spans this far; the cap only keeps a corrupt
    /// range from listing millions.
    /// </summary>
    public const int MaxMarks = 1000;

    /// <summary>The farthest tick listed, where a double still counts whole ticks exactly (2^53).</summary>
    private const double _maxTick = 9_007_199_254_740_992;

    /// <summary>The widest label step <see cref="MetresPerLabel"/> returns, past any real zoom.</summary>
    private const int _maxMetresPerLabel = 1_000_000;

    /// <summary>The label steps within each power of ten: 1, 2, 5, then 10, 20, 50, ….</summary>
    private static readonly int[] _labelSteps = [1, 2, 5];

    /// <summary>
    /// Labels come back closer only with this much more room than they need, so a zoom resting
    /// where the step changes does not make labels flicker in and out.
    /// </summary>
    private const double _closerLabelRoom = 1.2;

    // The long ticks' step and how many ticks share it, for labels metresPerLabel apart: within
    // the largest power of ten that divides the step, a 5 gets a long tick per label and four
    // minor ones between; anything else a long tick every power of ten and a minor one halfway.
    private static (long MetresPerMajor, int TicksPerMajor) Ticks(int metresPerLabel)
    {
        var decade = 1;
        while (metresPerLabel % (decade * 10) == 0)
        {
            decade *= 10;
        }

        return metresPerLabel / decade == 5 ? (metresPerLabel, 5) : (decade, 2);
    }

    /// <summary>A labelled metre's text, such as "3 m", or "-1 m" behind the start.</summary>
    public static UiText Label(long metre) => UiText.Format("{0} m", metre);

    /// <summary>
    /// The fewest metres between labels, 1, 2, 5, 10, 20, …, that leaves <paramref name="labelRoom"/>
    /// between label centres when a metre spans <paramref name="metreLength"/>; both in the same unit.
    /// A wider <paramref name="shown"/> step, the one on screen now, is kept until a closer step has
    /// <see cref="_closerLabelRoom"/> times the room.
    /// </summary>
    public static int MetresPerLabel(double metreLength, double labelRoom, int shown = 1)
    {
        var fit = FewestMetresPerLabel(metreLength, labelRoom);
        return shown > fit ? Math.Min(shown, FewestMetresPerLabel(metreLength, labelRoom * _closerLabelRoom)) : fit;
    }

    private static int FewestMetresPerLabel(double metreLength, double labelRoom)
    {
        if (!(metreLength > 0) || !(labelRoom > 0) || !double.IsFinite(labelRoom))
        {
            return 1;
        }

        for (var scale = 1; scale < _maxMetresPerLabel; scale *= 10)
        {
            foreach (var step in _labelSteps)
            {
                if (step * scale * metreLength >= labelRoom)
                {
                    return step * scale;
                }
            }
        }

        return _maxMetresPerLabel;
    }

    /// <summary>Fills <paramref name="marks"/> with every tick from <paramref name="left"/> to <paramref name="right"/>, in order.</summary>
    /// <param name="startX">Where 0 m is, in world units.</param>
    /// <param name="left">The left end of the range, in world units.</param>
    /// <param name="right">The right end of the range, in world units.</param>
    /// <param name="metresPerLabel">Labels every metre that is a multiple of this, from <see cref="MetresPerLabel"/>.</param>
    /// <param name="marks">Cleared, then filled, so a caller can reuse one list every frame.</param>
    public static void Fill(double startX, double left, double right, int metresPerLabel, List<RulerMark> marks)
    {
        ArgumentNullException.ThrowIfNull(marks);
        ArgumentOutOfRangeException.ThrowIfLessThan(metresPerLabel, 1);
        marks.Clear();
        if (!double.IsFinite(startX) || !double.IsFinite(left) || !double.IsFinite(right) || right < left)
        {
            return;
        }

        // Checked as doubles: a cast to long clamps a huge tick, which would slip past the cap.
        var (metresPerMajor, ticksPerMajor) = Ticks(metresPerLabel);
        var spacing = metresPerMajor * Metres.WorldUnitsPerMetre / ticksPerMajor;
        var first = Math.Ceiling((left - startX) / spacing);
        var last = Math.Floor((right - startX) / spacing);
        if (!(last - first < MaxMarks) || Math.Max(Math.Abs(first), Math.Abs(last)) > _maxTick)
        {
            return;
        }

        for (var tick = (long)first; tick <= (long)last; tick++)
        {
            var isMajor = tick % ticksPerMajor == 0;
            var metre = tick / ticksPerMajor * metresPerMajor;
            var labelMetre = isMajor && metre % metresPerLabel == 0 ? metre : (long?)null;
            marks.Add(new RulerMark(startX + (tick * spacing), isMajor, labelMetre));
        }
    }
}
