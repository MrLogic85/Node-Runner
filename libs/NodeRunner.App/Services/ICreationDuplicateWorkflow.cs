using NodeRunner.Domain;

namespace NodeRunner.App.Services;

public interface ICreationDuplicateWorkflow
{
    CreationDef Duplicate(Guid id, CreationDuplicateMode mode);
}
