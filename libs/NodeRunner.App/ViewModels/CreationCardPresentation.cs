using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

public sealed record CreationCardPresentation(
    Guid Id,
    string Name,
    string DisplayName,
    CreatureDef Creature,
    string SummaryText,
    string ThumbnailText,
    string SavedStateText,
    string UnlockCreditText,
    float AchievementProgress,
    string AchievementProgressText,
    bool IsExample,
    bool CanOpen,
    bool CanDuplicate,
    bool CanDelete);
