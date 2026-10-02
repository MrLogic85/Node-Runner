using System.Collections.Concurrent;
using NodeRunner.Domain;

namespace NodeRunner.App.Repositories;

public sealed class FileProgressionRepository : IProgressionRepository
{
    // Keyed by absolute path (not per-instance) so that even if more than
    // one FileProgressionRepository instance ever pointed at the same file
    // (SaveManager only ever constructs one today, but nothing enforces
    // that), a Load()'s read-validate-quarantine sequence still can't
    // interleave with another instance's Save() and quarantine away a file
    // that was actually just written successfully (#114).
    private static readonly ConcurrentDictionary<string, object> _pathLocks = new();

    private readonly string _path;
    private readonly object _writeLock;

    public FileProgressionRepository(IStorageLocation storageLocation)
    {
        ArgumentNullException.ThrowIfNull(storageLocation);
        _path = Path.Combine(storageLocation.DirectoryPath, "progression.json");
        _writeLock = _pathLocks.GetOrAdd(_path, static _ => new object());
    }

    public ProgressionDef Load()
    {
        // Shares _writeLock with Save() so a concurrent Save can never land
        // mid-read/mid-quarantine: without this, Quarantine() could rename
        // away a just-written valid file instead of the corrupt one it read
        // moments earlier (#114).
        lock (_writeLock)
        {
            if (!File.Exists(_path))
            {
                return new ProgressionDef();
            }

            try
            {
                // Strict loading (SaveJson) also rejects "{}": a missing field
                // must not silently read as a fresh progression (#114).
                return SaveJson.Deserialize<ProgressionDef>(File.ReadAllText(_path), _path);
            }
            catch (Exception ex) when (FilePersistenceExceptions.IsRecoverable(ex))
            {
                // A corrupt progression file must not crash startup seeding;
                // log it, quarantine it so it isn't reported again on the next
                // Load(), and recover to a fresh progression rather than
                // letting the exception propagate (#114).
                Console.Error.WriteLine(
                    $"[FileProgressionRepository] Progression file '{_path}' is corrupt, resetting to defaults: {ex}");
                Quarantine();
                return new ProgressionDef();
            }
        }
    }

    public void Save(ProgressionDef progression)
    {
        ArgumentNullException.ThrowIfNull(progression);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        var json = SaveJson.Serialize(progression);

        lock (_writeLock)
        {
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _path, overwrite: true);
        }
    }

    private void Quarantine()
    {
        try
        {
            var quarantinePath = $"{_path}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
            File.Move(_path, quarantinePath, overwrite: true);
        }
        catch (Exception ex)
        {
            // Best-effort: quarantining is a diagnostic nicety, not the
            // recovery itself (Load() already returns a fresh ProgressionDef
            // regardless). Its own failure modes are open-ended (permission,
            // read-only media, path-too-long, ...), so this catches broadly
            // and logs rather than letting a secondary failure escape the
            // recovery path it's supposed to be inside of (#114).
            Console.Error.WriteLine(
                $"[FileProgressionRepository] Could not quarantine corrupt progression file '{_path}': {ex}");
        }
    }
}
