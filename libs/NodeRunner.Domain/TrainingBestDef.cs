namespace NodeRunner.Domain;

/// <summary>
/// The best ever reached on a map, from any generation (#479). Unlike the latest run it never goes
/// down; the Training top bar's "Best" and, later, Stats show it.
/// </summary>
public sealed record TrainingBestDef
{
    public TrainingBestDef(int generation, double distance, string mapId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(generation, 1);
        if (!double.IsFinite(distance) || distance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distance), distance, "A best distance must be a finite, non-negative number.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);

        Generation = generation;
        Distance = distance;
        MapId = mapId;
    }

    /// <summary>The generation that reached it.</summary>
    public int Generation { get; }

    /// <summary>The furthest forward the creature's centre got, in world units.</summary>
    public double Distance { get; }

    public string MapId { get; }
}
