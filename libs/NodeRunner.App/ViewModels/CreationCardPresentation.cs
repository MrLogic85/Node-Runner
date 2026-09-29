using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

public sealed record CreationCardPresentation(
    Guid Id,
    string Name,
    CreatureDef Creature,
    string SummaryText,
    string ThumbnailText,
    string SavedStateText,
    string UnlockCreditText,
    float AchievementProgress,
    string AchievementProgressText,
    bool CanOpen,
    bool CanDuplicate,
    bool CanDelete)
{
    /// <summary>The part counts the card shows when the creature has nothing to draw.</summary>
    public static string ThumbnailTextFor(CreatureDef creature) =>
        $"{FormatCount(creature.Nodes.Count, "node")} · {FormatCount(creature.Beams.Count, "beam")} · {FormatCount(creature.Cores.Count, "core")}";

    private static string FormatCount(int count, string singular) =>
        count == 1
            ? $"1 {singular}"
            : $"{count} {singular}s";
}
