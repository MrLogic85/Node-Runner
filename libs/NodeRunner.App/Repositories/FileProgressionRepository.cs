using System.Collections.Concurrent;
using System.Text.Json;
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
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

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
                var json = File.ReadAllText(_path);

                // ProgressionDef's constructor parameters all have valid
                // defaults, so structurally incomplete JSON (e.g. "{}")
                // would otherwise silently deserialize as a fresh/reset
                // progression instead of being recognized as corrupt (#114).
                // Every file we write always contains the original unlock
                // fields as an object, so anything else didn't come from
                // Save() and must be treated as invalid. Newer fields stay
                // optional so older valid progression files can migrate
                // through the ProgressionDef constructor defaults.
                using (var document = JsonDocument.Parse(json))
                {
                    var root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object
                        || !HasProperty(root, nameof(ProgressionDef.ExtraCoreUnlocked))
                        || !HasProperty(root, nameof(ProgressionDef.ExtraCoreUnlockedAtGeneration)))
                    {
                        throw new InvalidDataException($"Progression file '{_path}' is missing required fields.");
                    }
                }

                return JsonSerializer.Deserialize<ProgressionDef>(json, _jsonOptions)
                    ?? throw new InvalidDataException($"Progression file '{_path}' is empty or invalid.");
            }
            catch (Exception ex) when (FilePersistenceExceptions.IsRecoverable(ex))
            {
                // A corrupt progression file must not crash training (it's
                // read on nearly every generation-completion tick); log it,
                // quarantine it so it isn't reported again on the next
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
        var json = JsonSerializer.Serialize(progression, _jsonOptions);

        lock (_writeLock)
        {
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _path, overwrite: true);
        }
    }

    private static bool HasProperty(JsonElement root, string name) =>
        root.EnumerateObject().Any(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase));

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
