using System.Text.Json.Serialization;

namespace NodeRunner.Domain;

/// <summary>
/// What the best run of a creation's latest finished generation achieved, and on which map. The
/// Creations card shows these values; they are measured during training, never recomputed.
/// <see cref="Distance"/> is the score; <see cref="ShownDistance"/> is the distance the player sees (#725).
/// </summary>
public sealed record TrainingRunDef
{
    public TrainingRunDef(double distance, double topSpeed, double elevation, string mapId, double? frontDistance = null)
    {
        RequireNonNegative(distance, nameof(distance));
        if (frontDistance is { } front)
        {
            RequireNonNegative(front, nameof(frontDistance));
        }
        RequireNonNegative(topSpeed, nameof(topSpeed));
        RequireNonNegative(elevation, nameof(elevation));
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);

        Distance = distance;
        TopSpeed = topSpeed;
        Elevation = elevation;
        MapId = mapId;
        FrontDistance = frontDistance;
    }

    /// <summary>
    /// The furthest the creature's centre got forward from its start; also the GA's score while
    /// distance is the only fitness (#137).
    /// </summary>
    public double Distance { get; }

    /// <summary>The highest forward speed of the creature's centre, averaged over half a second.</summary>
    public double TopSpeed { get; }

    /// <summary>The largest gap between the creature's lowest part and the ground beneath it.</summary>
    public double Elevation { get; }

    public string MapId { get; }

    /// <summary>
    /// How far ahead of its start the creature's front-most point was when the run ended (#725);
    /// null in a save from before it.
    /// </summary>
    public double? FrontDistance { get; }

    /// <summary>The distance shown for the run: <see cref="FrontDistance"/>, or <see cref="Distance"/> in an older save.</summary>
    [JsonIgnore]
    public double ShownDistance => FrontDistance ?? Distance;

    private static void RequireNonNegative(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "A training run value must be a finite, non-negative number.");
        }
    }
}
