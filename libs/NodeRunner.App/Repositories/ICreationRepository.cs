using NodeRunner.Domain;

namespace NodeRunner.App.Repositories;

public interface ICreationRepository
{
    IReadOnlyList<CreationDef> List();

    CreationDef? Get(Guid id);

    void Save(CreationDef creation);

    bool Delete(Guid id);
}
