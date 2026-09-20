using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

public sealed record CreationCardPresentation(
    Guid Id,
    string Name,
    CreatureDef Creature,
    string SummaryText,
    string NoteText,
    string ThumbnailText,
    string SavedStateText,
    string UnlockCreditText,
    float AchievementProgress,
    string AchievementProgressText,
    bool IsExample,
    bool CanOpen,
    bool CanEdit,
    bool CanDuplicate,
    bool CanDelete);
