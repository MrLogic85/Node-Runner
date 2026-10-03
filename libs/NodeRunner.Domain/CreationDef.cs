namespace NodeRunner.Domain;

/// <summary>
/// Durable player-owned creature. Training is optional so an untrained
/// Creation and a trained Creation share one persistence shape.
/// </summary>
public sealed record CreationDef
{
    public CreationDef(
        Guid id, string name, CreatureDef creature, TrainingStateDef? training = null, TrainSettingsDef? trainSettings = null)
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
        TrainSettings = trainSettings;
    }

    public Guid Id { get; }

    public string Name { get; }

    public CreatureDef Creature { get; }

    public TrainingStateDef? Training { get; }

    /// <summary>Train setup's last Start for this Creation; <c>null</c> until then (#617).</summary>
    public TrainSettingsDef? TrainSettings { get; }

    public CreationDef WithName(string name) => new(Id, name, Creature, Training, TrainSettings);

    public CreationDef WithTraining(TrainingStateDef? training) => new(Id, Name, Creature, training, TrainSettings);

    public CreationDef WithCreature(CreatureDef creature, TrainingStateDef? training) =>
        new(Id, Name, creature, training, TrainSettings);

    public CreationDef WithTrainSettings(TrainSettingsDef trainSettings) => new(Id, Name, Creature, Training, trainSettings);

    /// <summary>A new Creation with this one's body, training and Train setup values.</summary>
    public CreationDef CopyAs(Guid id, string name) => new(id, name, Creature, Training, TrainSettings);
}
