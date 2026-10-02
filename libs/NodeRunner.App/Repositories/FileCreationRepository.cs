using NodeRunner.Domain;

namespace NodeRunner.App.Repositories;

/// <summary>
/// Keeps each Creation in its own folder, <c>&lt;id&gt;/creation.json</c>, under the storage location.
/// The layout and every field are owned by docs/SAVE_FORMAT.md.
/// </summary>
public sealed class FileCreationRepository : ICreationRepository
{
    public const string CreationFileName = "creation.json";

    private readonly string _directoryPath;

    // Saves can now be dispatched from a background thread (see #113), so a
    // synchronous main-thread Save and a background one could otherwise race
    // on the same shared `.tmp` path. Serializing writes keeps the
    // write-temp-then-rename sequence atomic per repository instance.
    private readonly object _writeLock = new();

    public FileCreationRepository(IStorageLocation storageLocation)
    {
        ArgumentNullException.ThrowIfNull(storageLocation);
        _directoryPath = storageLocation.DirectoryPath;
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
        // A per-save unique name (rather than a shared "<path>.tmp") means
        // no two Save() calls, even from different repository instances,
        // can ever contend on the same temp path (#114).
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        var json = SaveJson.Serialize(creation);

        lock (_writeLock)
        {
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, path, overwrite: true);
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

    private static bool TryRead(string path, out CreationDef creation)
    {
        try
        {
            creation = SaveJson.Deserialize<CreationDef>(File.ReadAllText(path), path);
            return true;
        }
        catch (Exception ex) when (FilePersistenceExceptions.IsRecoverable(ex))
        {
            Console.Error.WriteLine($"[FileCreationRepository] Skipping unreadable creation file '{path}': {ex}");
            creation = null!;
            return false;
        }
    }

    private string FolderFor(Guid id) => Path.Combine(_directoryPath, id.ToString("N"));

    private string PathFor(Guid id) => Path.Combine(FolderFor(id), CreationFileName);
}
