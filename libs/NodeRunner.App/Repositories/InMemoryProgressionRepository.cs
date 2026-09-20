using NodeRunner.Domain;

namespace NodeRunner.App.Repositories;

public sealed class InMemoryProgressionRepository : IProgressionRepository
{
    private ProgressionDef _progression = new();

    public ProgressionDef Load() => _progression;

    public void Save(ProgressionDef progression)
    {
        ArgumentNullException.ThrowIfNull(progression);
        _progression = progression;
    }
}
