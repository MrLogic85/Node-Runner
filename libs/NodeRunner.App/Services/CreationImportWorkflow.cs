using NodeRunner.App.Repositories;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Services;

/// <summary>
/// Adds a shared build to Creations under a new id and the name the player gave it on
/// Import, without training or Train settings, so it trains from scratch, and on Settings' grid
/// default, since a share code holds no grid. Like a rename in Build,
/// the name is trimmed, cut to <see cref="NameLimits.Creation"/> and may match another creation's.
/// </summary>
public sealed class CreationImportWorkflow : ICreationImportWorkflow
{
    private readonly ICreationRepository _repository;
    private readonly Func<BuildGridDef> _defaultGrid;
    private readonly Func<Guid> _newId;

    public CreationImportWorkflow(ICreationRepository repository, Func<BuildGridDef> defaultGrid, Func<Guid>? newId = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(defaultGrid);
        _repository = repository;
        _defaultGrid = defaultGrid;
        _newId = newId ?? Guid.NewGuid;
    }

    public CreationDef Import(CreationDef build, string name)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var creation = new CreationDef(_newId(), NameLimits.Fit(name.Trim(), NameLimits.Creation), build.Creature) { Grid = _defaultGrid() };
        _repository.Save(creation);
        return creation;
    }
}
