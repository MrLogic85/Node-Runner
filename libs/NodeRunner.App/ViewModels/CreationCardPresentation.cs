using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// One card on Creations or Examples. <see cref="SummaryText"/> is the line under the name; a
/// trained creation shows <see cref="Training"/> there instead and leaves it empty.
/// </summary>
public sealed record CreationCardPresentation(
    Guid Id,
    string Name,
    CreatureDef Creature,
    string SummaryText,
    string ThumbnailText,
    CreationCardTraining? Training,
    bool CanOpen,
    bool CanDuplicate,
    bool CanDelete)
{
    /// <summary>The part counts the card shows when the creature has nothing to draw.</summary>
    public static string ThumbnailTextFor(CreatureDef creature) =>
        $"{FormatCount(creature.Nodes.Count, "node")} · {FormatCount(creature.Beams.Count, "beam")} · {FormatCount(creature.Sensors.Count, "sensor")}";

    internal static string FormatCount(int count, string singular) =>
        count == 1
            ? $"1 {singular}"
            : $"{count} {singular}s";
}

/// <summary>
/// What a trained card shows of its latest training (#479): the latest generation's best run's
/// distance and elevation in metres and top speed in m/s, all without units, the map it ran on and
/// how many generations it has trained. These can go down; the best ever belongs to Stats.
/// </summary>
public sealed record CreationCardTraining(
    string DistanceText,
    string TopSpeedText,
    string ElevationText,
    string MapId,
    string GenerationsText)
{
    public static CreationCardTraining From(TrainingStateDef training)
    {
        ArgumentNullException.ThrowIfNull(training);
        var run = training.Latest;
        return new CreationCardTraining(
            Metres.Format(run.ShownDistance),
            Metres.Format(run.TopSpeed),
            Metres.Format(run.Elevation),
            run.MapId,
            CreationCardPresentation.FormatCount(training.Generation, "generation"));
    }
}
