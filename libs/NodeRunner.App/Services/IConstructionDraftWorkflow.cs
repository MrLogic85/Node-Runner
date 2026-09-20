using NodeRunner.Domain;

namespace NodeRunner.App.Services;

public interface IConstructionDraftWorkflow
{
    ConstructionDraftSession BeginRebuildDraft(CreatureDef sourceCreature);

    CreationDef CompleteDraft(CreatureDef creature, string name);
}
