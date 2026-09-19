namespace NodeRunner.Domain;

/// <summary>
/// Durable player-owned creature. Training is optional so an untrained
/// Creation and a trained Creation share one persistence shape.
/// </summary>
public sealed record CreationDef
{
    public CreationDef(Guid id, string name, CreatureDef creature, TrainingStateDef? training = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A Creation needs a non-empty identifier.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(creature);

        Id = id;
        Name = name;
        Creature = creature;
        Training = training;
    }

    public Guid Id { get; }

    public string Name { get; }

    public CreatureDef Creature { get; }

    public TrainingStateDef? Training { get; }
}
