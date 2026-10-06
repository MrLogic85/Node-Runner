using System.Text.Json.Nodes;
using NodeRunner.Domain;

namespace NodeRunner.App.Repositories;

/// <summary>
/// Keeps each Creation in its own folder, <c>&lt;id&gt;/creation.json</c>, under the storage location.
/// The layout and every field are owned by docs/SAVE_FORMAT.md.
/// </summary>
public sealed class FileCreationRepository : ICreationRepository
{
    public const string CreationFileName = "creation.json";

    /// <summary>
    /// <c>creation.json</c>'s versions. Add a migration here when its shape changes
    /// (docs/SAVE_FORMAT.md → "Versions and migration").
    /// </summary>
    public static VersionedSaveFile<CreationDef> Format { get; } = new([AddServosArray]);

    private readonly string _directoryPath;
    private readonly VersionedSaveFile<CreationDef> _format;

    // Saves can now be dispatched from a background thread (see #113), so a
    // synchronous main-thread Save and a background one could otherwise race
    // on the same shared `.tmp` path. Serializing writes keeps the
    // write-temp-then-rename sequence atomic per repository instance.
    private readonly object _writeLock = new();

    public FileCreationRepository(IStorageLocation storageLocation)
        : this(storageLocation, Format)
    {
    }

    /// <summary>Uses <paramref name="format"/> instead of <see cref="Format"/>, so tests can add migrations.</summary>
    public FileCreationRepository(IStorageLocation storageLocation, VersionedSaveFile<CreationDef> format)
    {
        ArgumentNullException.ThrowIfNull(storageLocation);
        ArgumentNullException.ThrowIfNull(format);
        _directoryPath = storageLocation.DirectoryPath;
        _format = format;
    }

    private static void AddServosArray(JsonObject file)
    {
        if (file["creature"] is not JsonObject creature)
        {
            throw new InvalidDataException("creation.json is missing its creature object.");
        }

        creature["servos"] ??= new JsonArray();
    }

    public IReadOnlyList<CreationDef> List()
    {
        if (!Directory.Exists(_directoryPath))
        {
            return [];
        }

        // A single unreadable/corrupt file must not empty the whole
        // Creations list (#114): skip and log it instead of letting
        // Read()'s exception propagate out of the LINQ pipeline.
        var creations = new List<CreationDef>();
        foreach (var folder in Directory.EnumerateDirectories(_directoryPath))
        {
            var path = Path.Combine(folder, CreationFileName);
            if (File.Exists(path) && TryRead(path, out var creation))
            {
                creations.Add(creation);
            }
        }

        return creations.OrderBy(creation => creation.Name).ToArray();
    }

    public CreationDef? Get(Guid id)
    {
        var path = PathFor(id);
        if (!File.Exists(path))
        {
            return null;
        }

        // Matches List()'s recoverable-corruption handling (#114): treat an
        // unreadable file the same as "no Creation with this id" instead of
        // throwing, so a lookup for the currently open Creation can't crash
        // a caller if its file becomes corrupt between saves.
        return TryRead(path, out var creation) ? creation : null;
    }

    public void Save(CreationDef creation)
    {
        ArgumentNullException.ThrowIfNull(creation);
        Directory.CreateDirectory(FolderFor(creation.Id));

        var path = PathFor(creation.Id);
        var json = _format.Serialize(creation);

        lock (_writeLock)
        {
            WriteAtomically(path, json);
        }
    }

    public bool Delete(Guid id)
    {
        var folder = FolderFor(id);
        if (!Directory.Exists(folder))
        {
            return false;
        }

        Directory.Delete(folder, recursive: true);
        return true;
    }

    // A file this app can't read, including one from a newer version, is skipped and never written.
    private bool TryRead(string path, out CreationDef creation)
    {
        string json;
        VersionedLoad<CreationDef> load;
        try
        {
            json = File.ReadAllText(path);
            load = _format.Deserialize(json, path);
        }
        catch (Exception ex) when (FilePersistenceExceptions.IsRecoverable(ex))
        {
            Console.Error.WriteLine($"[FileCreationRepository] Skipping unreadable creation file '{path}': {ex}");
            creation = null!;
            return false;
        }

        creation = load.Value;
        if (load.IsOutdated)
        {
            WriteBack(path, json, creation);
        }

        return true;
    }

    // Writes an outdated file back in the current version. Skipped if a Save replaced the file after
    // it was read, so a load never overwrites newer content; a failed write only leaves the file
    // outdated, to migrate again next time.
    private void WriteBack(string path, string readJson, CreationDef creation)
    {
        try
        {
            var json = _format.Serialize(creation);
            lock (_writeLock)
            {
                if (File.ReadAllText(path) == readJson)
                {
                    WriteAtomically(path, json);
                }
            }
        }
        catch (Exception ex) when (FilePersistenceExceptions.IsRecoverable(ex))
        {
            Console.Error.WriteLine($"[FileCreationRepository] Could not write back migrated creation file '{path}': {ex}");
        }
    }

    // A per-save unique temp name (rather than a shared "<path>.tmp") means no two writes, even from
    // different repository instances, can ever contend on the same temp path (#114).
    private static void WriteAtomically(string path, string json)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, path, overwrite: true);
    }

    private string FolderFor(Guid id) => Path.Combine(_directoryPath, id.ToString("N"));

    private string PathFor(Guid id) => Path.Combine(FolderFor(id), CreationFileName);
}
