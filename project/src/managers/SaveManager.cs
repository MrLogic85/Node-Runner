using Godot;
using NodeRunner.App.Repositories;
using NodeRunner.Domain;

namespace NodeRunner.Managers;

/// <summary>Composition-root facade for durable Creations.</summary>
public partial class SaveManager : Node
{
    private ICreationRepository? _repository;
    private IProgressionRepository? _progressionRepository;

    public override void _Ready()
    {
        var directory = ProjectSettings.GlobalizePath("user://creations");
        _repository = new FileCreationRepository(new GodotStorageLocation(directory));
        var progressionDirectory = ProjectSettings.GlobalizePath("user://progression");
        _progressionRepository = new FileProgressionRepository(new GodotStorageLocation(progressionDirectory));
    }

    public IReadOnlyList<CreationDef> List()
    {
        return Repository.List();
    }

    public void Save(CreationDef creation)
    {
        Repository.Save(creation);
    }

    public bool Delete(Guid id)
    {
        return Repository.Delete(id);
    }

    public CreationDef? Get(Guid id)
    {
        return Repository.Get(id);
    }

    public void ResetTraining(Guid id)
    {
        var source = Repository.Get(id) ?? throw new InvalidOperationException($"Creation '{id}' was not found.");
        Repository.Save(new CreationDef(source.Id, source.Name, source.Creature));
    }

    public ProgressionDef Progression => ProgressionRepository.Load();

    public bool UnlockExtraCore(int generation)
    {
        var current = ProgressionRepository.Load();
        if (current.ExtraCoreUnlocked)
        {
            return false;
        }

        ProgressionRepository.Save(new ProgressionDef(true, generation));
        return true;
    }

    public CreationDef Duplicate(Guid id)
    {
        var source = Repository.Get(id) ?? throw new InvalidOperationException($"Creation '{id}' was not found.");
        var copy = new CreationDef(Guid.NewGuid(), $"{source.Name} Copy", source.Creature, source.Training);
        Repository.Save(copy);
        return copy;
    }

    private ICreationRepository Repository =>
        _repository ?? throw new InvalidOperationException("SaveManager is not ready.");

    private IProgressionRepository ProgressionRepository =>
        _progressionRepository ?? throw new InvalidOperationException("SaveManager is not ready.");

    private sealed class GodotStorageLocation(string directoryPath) : IStorageLocation
    {
        public string DirectoryPath { get; } = directoryPath;
    }
}
