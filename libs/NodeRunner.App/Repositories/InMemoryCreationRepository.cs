using NodeRunner.Domain;

namespace NodeRunner.App.Repositories;

public sealed class InMemoryCreationRepository : ICreationRepository
{
    private readonly Dictionary<Guid, CreationDef> _creations = [];

    public IReadOnlyList<CreationDef> List() => _creations.Values.OrderBy(creation => creation.Name).ToArray();

    public CreationDef? Get(Guid id) => _creations.GetValueOrDefault(id);

    public void Save(CreationDef creation)
    {
        ArgumentNullException.ThrowIfNull(creation);
        _creations[creation.Id] = creation;
    }

    public bool Delete(Guid id) => _creations.Remove(id);
}
