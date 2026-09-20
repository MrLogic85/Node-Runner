using NodeRunner.Domain;

namespace NodeRunner.App.Repositories;

public interface IProgressionRepository
{
    ProgressionDef Load();

    void Save(ProgressionDef progression);
}
