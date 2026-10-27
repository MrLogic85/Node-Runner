using System.Text.Json.Serialization;

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

    /// <summary>Train setup's last Start for this Creation; <c>null</c> until then.</summary>
    public TrainSettingsDef? TrainSettings { get; }

    /// <summary>
    /// Build's grid for this Creation, copied from Settings' default when it is made, so a later
    /// change to the default leaves it as it is.
    /// </summary>
    [JsonRequired]
    public BuildGridDef Grid
    {
        get;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = BuildGridDef.Default;

    public CreationDef WithName(string name) => new(Id, name, Creature, Training, TrainSettings) { Grid = Grid };

    public CreationDef WithTraining(TrainingStateDef? training) => new(Id, Name, Creature, training, TrainSettings) { Grid = Grid };

    public CreationDef WithCreature(CreatureDef creature, TrainingStateDef? training) =>
        new(Id, Name, creature, training, TrainSettings) { Grid = Grid };

    public CreationDef WithTrainSettings(TrainSettingsDef trainSettings) => new(Id, Name, Creature, Training, trainSettings) { Grid = Grid };

    /// <summary>A new Creation with this one's body, training, Train setup values and grid.</summary>
    public CreationDef CopyAs(Guid id, string name) => new(id, name, Creature, Training, TrainSettings) { Grid = Grid };
}
