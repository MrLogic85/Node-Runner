namespace NodeRunner.App.ViewModels;

public enum CreationCommandKind
{
    Open,
    Duplicate,
    Delete,
}

public sealed record CreationCommandIntent(CreationCommandKind Kind, Guid CreationId);
