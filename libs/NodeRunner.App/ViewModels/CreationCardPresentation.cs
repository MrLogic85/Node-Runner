using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// One card on Creations or Examples. <see cref="SummaryText"/> is the line under the name; a
/// trained creation shows <see cref="Training"/> there instead and has none.
/// </summary>
public sealed record CreationCardPresentation(
    Guid Id,
    UiText Name,
    CreatureDef Creature,
    UiText? SummaryText,
    UiText ThumbnailText,
    CreationCardTraining? Training,
    bool CanOpen,
    bool CanDuplicate,
    bool CanDelete)
{
    /// <summary>The part counts the card shows when the creature has nothing to draw.</summary>
    public static UiText ThumbnailTextFor(CreatureDef creature) =>
        UiText.Format(
            "{0} · {1} · {2}",
            UiText.Counted("{0} node", "{0} nodes", creature.Nodes.Count),
            UiText.Counted("{0} beam", "{0} beams", creature.Beams.Count),
            UiText.Counted("{0} sensor", "{0} sensors", creature.Sensors.Count));
}

/// <summary>
/// What a trained card shows of its latest training (#479): the latest generation's best run's
/// distance and elevation in metres and top speed in m/s, all without units, the map it ran on and
/// how many generations it has trained. These can go down; the best ever belongs to Stats.
/// </summary>
public sealed record CreationCardTraining(
    UiText DistanceText,
    UiText TopSpeedText,
    UiText ElevationText,
    string MapId,
    UiText GenerationsText)
{
    public static CreationCardTraining From(TrainingStateDef training)
    {
        ArgumentNullException.ThrowIfNull(training);
        var run = training.Latest;
        return new CreationCardTraining(
            UiText.Number(Metres.Number(run.ShownDistance)),
            UiText.Number(Metres.Number(run.TopSpeed)),
            UiText.Number(Metres.Number(run.Elevation)),
            run.MapId,
            UiText.Counted("{0} generation", "{0} generations", training.Generation));
    }
}
