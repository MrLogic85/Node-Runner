using NodeRunner.Domain;

namespace NodeRunner.App.Services;

/// <summary>Owns the saved-Creation identity rules for Build/Rebuild drafts.</summary>
public sealed class ConstructionDraftWorkflow : IConstructionDraftWorkflow
{
    private readonly Func<Guid> _newId;

    public ConstructionDraftWorkflow(Func<Guid>? newId = null)
    {
        _newId = newId ?? Guid.NewGuid;
    }

    public ConstructionDraftSession BeginRebuildDraft(CreatureDef sourceCreature)
    {
        ArgumentNullException.ThrowIfNull(sourceCreature);
        return new ConstructionDraftSession(
            sourceCreature,
            ActiveCreationId: null,
            "Rebuild creates a new Creation; previous training will not be copied.");
    }

    public CreationDef CompleteDraft(CreatureDef creature, string name)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new CreationDef(_newId(), name, creature);
    }
}
