using NodeRunner.Domain;

namespace NodeRunner.App.Services;

public interface IConstructionEditWorkflow
{
    ConstructionEditResult PersistMoveOnlyEdit(Guid activeCreationId, CreatureDef editedCreature);
}
