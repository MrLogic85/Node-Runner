namespace NodeRunner.Domain;

/// <summary>
/// Durable player-owned creature. Training is optional so an untrained
/// Creation and a trained Creation share one persistence shape. A Creation
/// locks when its first training session finishes (#369).
/// </summary>
public sealed record CreationDef
{
    public CreationDef(Guid id, string name, CreatureDef creature, TrainingStateDef? training = null)
        : this(id, name, creature, BrainShapeDef.Default, training)
    {
    }

    [System.Text.Json.Serialization.JsonConstructor]
    public CreationDef(
        Guid id,
        string name,
        CreatureDef creature,
        BrainShapeDef? brainShape,
        TrainingStateDef? training = null,
        bool isLocked = false)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A Creation needs a non-empty identifier.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(creature);
        if (isLocked && training is null)
        {
            throw new ArgumentException("Only a trained Creation can be locked.", nameof(isLocked));
        }

        Id = id;
        Name = name;
        Creature = creature;
        BrainShape = ResolveBrainShape(brainShape, training);
        Training = training;
        IsLocked = isLocked;
    }

    public Guid Id { get; }

    public string Name { get; }

    public CreatureDef Creature { get; }

    public BrainShapeDef BrainShape { get; }

    public TrainingStateDef? Training { get; }

    /// <summary>
    /// Set once a training session finishes with at least one finished generation in the
    /// Creation's training: anatomy and brain shape stay as the trained brain needs them.
    /// Starting or pausing does not set it; resetting the training clears it. When a session
    /// counts as finished is owned by the App layer (docs/TRAINING_LOOP.md).
    /// </summary>
    public bool IsLocked { get; }

    private static BrainShapeDef ResolveBrainShape(BrainShapeDef? brainShape, TrainingStateDef? training)
    {
        if (brainShape is not null)
        {
            return brainShape;
        }

        if (training?.LayerSizes is { Length: >= 3 } layerSizes)
        {
            var hidden = layerSizes.Skip(1).Take(layerSizes.Length - 2).ToArray();
            if (hidden.Length is >= BrainShapeDef.MinimumHiddenLayers and <= BrainShapeDef.MaximumHiddenLayers
                && hidden.Distinct().Count() == 1
                && hidden[0] is >= BrainShapeDef.MinimumNeuronsPerLayer and <= BrainShapeDef.MaximumNeuronsPerLayer)
            {
                return new BrainShapeDef(hidden.Length, hidden[0]);
            }
        }

        return BrainShapeDef.Default;
    }
}
