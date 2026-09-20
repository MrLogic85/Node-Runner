namespace NodeRunner.App.ViewModels;

public enum CreationCommandKind
{
    Open,
    Edit,
    Duplicate,
    Delete,
}

public sealed record CreationCommandIntent(CreationCommandKind Kind, Guid CreationId);
