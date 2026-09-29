using NodeRunner.Domain;

namespace NodeRunner.App.Services;

public interface IExampleCopyWorkflow
{
    CreationDef Copy(Guid exampleId);
}
