namespace NodeRunner.Domain;

/// <summary>
/// What every part of a creature has (#1221): an id unique across all its parts, the name the player gave it,
/// and its concrete kind, which <see cref="PartKinds"/> describes. Not saved: each part's record is.
/// </summary>
public interface IPartDef
{
    int Id { get; }

    string? Name { get; }

    PartKind PartKind { get; }
}
