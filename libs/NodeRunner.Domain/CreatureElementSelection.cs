namespace NodeRunner.Domain;

public sealed record CreatureElementSelection
{
    public CreatureElementSelection(CreatureElementKind kind, int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Creature element index must be non-negative.");
        }

        Kind = kind;
        Index = index;
    }

    public CreatureElementKind Kind { get; }

    public int Index { get; }
}
