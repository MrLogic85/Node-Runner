namespace NodeRunner.Domain;

/// <summary>
/// The best ever reached on a map, from any generation. Unlike the latest run its score never
/// goes down; the Training arena's best marker shows it. <see cref="Distance"/>
/// is the score that decides the best; <see cref="FrontDistance"/> is what the marker shows.
/// </summary>
public sealed record TrainingBestDef
{
    public TrainingBestDef(int generation, double distance, string mapId, double? frontDistance = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(generation, 1);
        if (!double.IsFinite(distance) || distance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distance), distance, "A best distance must be a finite, non-negative number.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);
        if (frontDistance is { } front && (!double.IsFinite(front) || front < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(frontDistance), front, "A best front distance must be a finite, non-negative number.");
        }

        Generation = generation;
        Distance = distance;
        MapId = mapId;
        FrontDistance = frontDistance;
    }

    /// <summary>The generation that reached it.</summary>
    public int Generation { get; }

    /// <summary>The furthest forward the creature's centre got, in world units.</summary>
    public double Distance { get; }

    public string MapId { get; }

    /// <summary>
    /// The furthest any latest run's front-most point ended ahead of its start on this map,
    /// whichever run holds the score, so it never goes down. Null in an older save until
    /// its next generation is saved.
    /// </summary>
    public double? FrontDistance { get; }
}
