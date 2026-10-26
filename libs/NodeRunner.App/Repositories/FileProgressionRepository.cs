using NodeRunner.Domain;

namespace NodeRunner.App.Repositories;

public sealed class FileProgressionRepository : IProgressionRepository
{
    /// <summary>
    /// <c>progression.json</c>'s versions. Add a migration here when its shape changes
    /// (docs/SAVE_FORMAT.md → "Versions and migration").
    /// </summary>
    public static VersionedSaveFile<ProgressionDef> Format { get; } = new([]);

    private readonly VersionedFileStore<ProgressionDef> _store;

    public FileProgressionRepository(IStorageLocation storageLocation)
        : this(storageLocation, Format)
    {
    }

    /// <summary>Uses <paramref name="format"/> instead of <see cref="Format"/>, so tests can add migrations.</summary>
    public FileProgressionRepository(IStorageLocation storageLocation, VersionedSaveFile<ProgressionDef> format)
    {
        ArgumentNullException.ThrowIfNull(storageLocation);
        ArgumentNullException.ThrowIfNull(format);
        _store = new VersionedFileStore<ProgressionDef>(
            Path.Combine(storageLocation.DirectoryPath, "progression.json"),
            format,
            static () => new ProgressionDef(),
            nameof(FileProgressionRepository));
    }

    public ProgressionDef Load() => _store.Load();

    public void Save(ProgressionDef progression) => _store.Save(progression);
}
