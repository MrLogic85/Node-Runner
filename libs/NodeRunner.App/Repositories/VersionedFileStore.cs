using System.Collections.Concurrent;

namespace NodeRunner.App.Repositories;

/// <summary>
/// One versioned save file that loads fresh when it is missing or fails to load, as
/// <c>progression.json</c> and <c>settings.json</c> do (docs/SAVE_FORMAT.md → "Writing and reading").
/// A file that fails to load is logged and quarantined, never overwritten by a migration.
/// </summary>
internal sealed class VersionedFileStore<T>
    where T : class
{
    // Keyed by absolute path (not per-instance) so that even if more than
    // one store pointed at the same file, a Load()'s read-validate-quarantine
    // sequence still can't interleave with another instance's Save() and
    // quarantine away a file that was actually just written successfully.
    private static readonly ConcurrentDictionary<string, object> _pathLocks = new();

    private readonly string _path;
    private readonly object _writeLock;
    private readonly VersionedSaveFile<T> _format;
    private readonly Func<T> _fresh;
    private readonly string _owner;

    /// <param name="path">The file.</param>
    /// <param name="format">The file's versions.</param>
    /// <param name="fresh">What a missing or unloadable file loads as.</param>
    /// <param name="owner">The repository's name, for the log.</param>
    public VersionedFileStore(string path, VersionedSaveFile<T> format, Func<T> fresh, string owner)
    {
        _path = path;
        _writeLock = _pathLocks.GetOrAdd(_path, static _ => new object());
        _format = format;
        _fresh = fresh;
        _owner = owner;
    }

    public T Load()
    {
        // Shares _writeLock with Save() so a concurrent Save can never land
        // mid-read/mid-quarantine: without this, Quarantine() could rename
        // away a just-written valid file instead of the corrupt one it read
        // moments earlier.
        lock (_writeLock)
        {
            if (!File.Exists(_path))
            {
                return _fresh();
            }

            VersionedLoad<T> load;
            try
            {
                // Strict loading (SaveJson) also rejects "{}": a missing field
                // must not silently read as fresh. A file newer than the app,
                // or one a migration can't change, fails here too and is
                // quarantined, never overwritten.
                load = _format.Deserialize(File.ReadAllText(_path), _path);
            }
            catch (Exception ex) when (FilePersistenceExceptions.IsRecoverable(ex))
            {
                // A corrupt file must not crash startup; log it, quarantine it
                // so it isn't reported again on the next Load(), and recover to
                // a fresh value rather than letting the exception propagate.
                Console.Error.WriteLine($"[{_owner}] File '{_path}' is corrupt, resetting to defaults: {ex}");
                Quarantine();
                return _fresh();
            }

            if (load.IsOutdated)
            {
                WriteBack(load.Value);
            }

            return load.Value;
        }
    }

    public void Save(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var json = _format.Serialize(value);

        lock (_writeLock)
        {
            WriteAtomically(json);
        }
    }

    // Called under _writeLock, so no Save can land between the read and this write. A failed write
    // only leaves the file outdated, to migrate again next time.
    private void WriteBack(T value)
    {
        try
        {
            WriteAtomically(_format.Serialize(value));
        }
        catch (Exception ex) when (FilePersistenceExceptions.IsRecoverable(ex))
        {
            Console.Error.WriteLine($"[{_owner}] Could not write back migrated file '{_path}': {ex}");
        }
    }

    private void WriteAtomically(string json)
    {
        var temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _path, overwrite: true);
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
            // recovery itself (Load() already returns a fresh value
            // regardless). Its own failure modes are open-ended (permission,
            // read-only media, path-too-long, ...), so this catches broadly
            // and logs rather than letting a secondary failure escape the
            // recovery path it's supposed to be inside of.
            Console.Error.WriteLine($"[{_owner}] Could not quarantine corrupt file '{_path}': {ex}");
        }
    }
}
