namespace NodeRunner.Domain;

public sealed record CreatureElementSelection
{
    public CreatureElementSelection(CreatureElementKind kind, int id)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Creature element id must be positive.");
        }

        Kind = kind;
        Id = id;
    }

    public CreatureElementKind Kind { get; }

    public int Id { get; }
}
