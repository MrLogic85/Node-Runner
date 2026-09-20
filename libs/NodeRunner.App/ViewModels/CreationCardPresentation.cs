namespace NodeRunner.App.ViewModels;

public sealed record CreationCardPresentation(
    Guid Id,
    string Name,
    string SummaryText,
    string NoteText,
    bool CanOpen,
    bool CanEdit,
    bool CanDuplicate,
    bool CanDelete);
