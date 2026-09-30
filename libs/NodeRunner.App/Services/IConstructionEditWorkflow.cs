using NodeRunner.Domain;

namespace NodeRunner.App.Services;

public interface IConstructionEditWorkflow
{
    ConstructionEditResult PersistEdit(Guid activeCreationId, CreatureDef editedCreature, BrainShapeDef brainShape, bool moveOnly);
}
