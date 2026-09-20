namespace NodeRunner.App.ViewModels;

public sealed record CreationCardPresentation(
    Guid Id,
    string Name,
    string SummaryText,
    string NoteText,
    string ThumbnailText,
    string SavedStateText,
    string UnlockCreditText,
    bool CanOpen,
    bool CanEdit,
    bool CanDuplicate,
    bool CanDelete);
