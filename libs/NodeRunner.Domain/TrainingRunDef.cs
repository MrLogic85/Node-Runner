namespace NodeRunner.Domain;

/// <summary>
/// What the winning run of a creation's latest training achieved, and on which map. The Creations
/// card shows these values; they are measured during training, never recomputed.
/// </summary>
public sealed record TrainingRunDef
{
    public TrainingRunDef(double distance, double topSpeed, double elevation, string mapId)
    {
        RequireNonNegative(distance, nameof(distance));
        RequireNonNegative(topSpeed, nameof(topSpeed));
        RequireNonNegative(elevation, nameof(elevation));
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);

        Distance = distance;
        TopSpeed = topSpeed;
        Elevation = elevation;
        MapId = mapId;
    }

    /// <summary>
    /// The furthest the creature's centre got forward from its start. This is what the card shows;
    /// <see cref="TrainingStateDef.BestFitness"/> is the GA's score, which equals this distance today
    /// but may change if the fitness function does.
    /// </summary>
    public double Distance { get; }

    /// <summary>The highest forward speed of the creature's centre, averaged over half a second.</summary>
    public double TopSpeed { get; }

    /// <summary>The largest gap between the creature's lowest part and the ground beneath it.</summary>
    public double Elevation { get; }

    public string MapId { get; }

    private static void RequireNonNegative(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "A training run value must be a finite, non-negative number.");
        }
    }
}
