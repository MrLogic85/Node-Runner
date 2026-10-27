using System.Text.Json.Serialization;

namespace NodeRunner.Domain;

/// <summary>
/// Build's grid: its cell size and whether placed and moved joints snap to it. A size off
/// <see cref="SizesCm"/> is moved to the nearest, never rejected, so a hand-edited file still loads.
/// </summary>
public sealed record BuildGridDef
{
    /// <summary>The grid sizes the player can pick, in cm, smallest first.</summary>
    public static IReadOnlyList<int> SizesCm { get; } = [25, 50, 100];

    /// <summary>The grid size in cm of <see cref="Default"/>: half a metre.</summary>
    public const int DefaultSizeCm = 50;

    /// <summary>Half a metre, without snapping: the grid a fresh install gives new Creations.</summary>
    public static BuildGridDef Default { get; } = new(DefaultSizeCm, snap: false);

    /// <param name="sizeCm">The cell size in cm, moved to the nearest of <see cref="SizesCm"/>.</param>
    /// <param name="snap">Whether placed and moved joints land on the grid.</param>
    public BuildGridDef(int sizeCm, bool snap)
    {
        SizeCm = sizeCm;
        Snap = snap;
    }

    /// <summary>The nearest of <see cref="SizesCm"/>; a tie goes to the larger.</summary>
    [JsonRequired]
    public int SizeCm
    {
        get;
        init => field = NearestSizeCm(value);
    }

    [JsonRequired]
    public bool Snap { get; init; }

    /// <summary>The nearest of <see cref="SizesCm"/> to <paramref name="sizeCm"/>; a tie goes to the larger.</summary>
    public static int NearestSizeCm(int sizeCm) => SizesCm.MinBy(size => (Math.Abs((long)sizeCm - size), -size));
}
