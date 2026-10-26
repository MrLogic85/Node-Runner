using NodeRunner.Domain;

namespace NodeRunner.App.Repositories;

public sealed class FileSettingsRepository : ISettingsRepository
{
    public const string FileName = "settings.json";

    /// <summary>
    /// <c>settings.json</c>'s versions. Add a migration here when its shape changes
    /// (docs/SAVE_FORMAT.md → "Versions and migration").
    /// </summary>
    public static VersionedSaveFile<SettingsDef> Format { get; } = new([]);

    private readonly VersionedFileStore<SettingsDef> _store;

    public FileSettingsRepository(IStorageLocation storageLocation)
    {
        ArgumentNullException.ThrowIfNull(storageLocation);
        _store = new VersionedFileStore<SettingsDef>(
            Path.Combine(storageLocation.DirectoryPath, FileName),
            Format,
            static () => SettingsDef.Default,
            nameof(FileSettingsRepository));
    }

    public SettingsDef Load() => _store.Load();

    public void Save(SettingsDef settings) => _store.Save(settings);
}
