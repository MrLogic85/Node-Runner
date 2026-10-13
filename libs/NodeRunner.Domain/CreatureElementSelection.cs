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

    /// <summary><paramref name="part"/> as it is selected (#1221).</summary>
    public static CreatureElementSelection Of(IPartDef part)
    {
        ArgumentNullException.ThrowIfNull(part);
        return new(PartKinds.Of(part.PartKind).Element, part.Id);
    }

    public CreatureElementKind Kind { get; }

    public int Id { get; }
}
