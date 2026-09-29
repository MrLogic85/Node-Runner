using System.Globalization;
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
        $"{FormatCount(creature.Nodes.Count, "node")} · {FormatCount(creature.Beams.Count, "beam")} · {FormatCount(creature.Cores.Count, "core")}";

    internal static string FormatCount(int count, string singular) =>
        count == 1
            ? $"1 {singular}"
            : $"{count} {singular}s";
}

/// <summary>
/// What a trained card shows of its latest training: the best run's distance, top speed and
/// elevation without units, the map it ran on and how many generations it has trained.
/// </summary>
public sealed record CreationCardTraining(
    string DistanceText,
    string TopSpeedText,
    string ElevationText,
    string? MapId,
    string GenerationsText)
{
    private const string _unknown = "—";

    /// <summary>Training saved before runs were recorded has no <see cref="TrainingStateDef.BestRun"/>; its values read "—".</summary>
    public static CreationCardTraining From(TrainingStateDef training)
    {
        ArgumentNullException.ThrowIfNull(training);
        var run = training.BestRun;
        return new CreationCardTraining(
            Format(run?.Distance),
            Format(run?.TopSpeed),
            Format(run?.Elevation),
            run?.MapId,
            CreationCardPresentation.FormatCount(training.Generation, "generation"));
    }

    private static string Format(double? value) =>
        value is { } number
            ? number.ToString("0.0", CultureInfo.InvariantCulture)
            : _unknown;
}
