using System.Text.Json;
using NodeRunner.Domain;

namespace NodeRunner.App.Repositories;

public sealed class FileCreationRepository : ICreationRepository
{
    private readonly string _directoryPath;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

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

        return Directory.EnumerateFiles(_directoryPath, "*.json")
            .Select(path => Read(path))
            .OrderBy(creation => creation.Name)
            .ToArray();
    }

    public CreationDef? Get(Guid id)
    {
        var path = PathFor(id);
        return File.Exists(path) ? Read(path) : null;
    }

    public void Save(CreationDef creation)
    {
        ArgumentNullException.ThrowIfNull(creation);
        Directory.CreateDirectory(_directoryPath);

        var path = PathFor(creation.Id);
        var temporaryPath = $"{path}.tmp";
        var json = JsonSerializer.Serialize(creation, _jsonOptions);

        lock (_writeLock)
        {
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, path, overwrite: true);
        }
    }

    public bool Delete(Guid id)
    {
        var path = PathFor(id);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    private CreationDef Read(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<CreationDef>(json, _jsonOptions)
            ?? throw new InvalidDataException($"Creation file '{path}' is empty or invalid.");
    }

    private string PathFor(Guid id) => Path.Combine(_directoryPath, $"{id:N}.json");
}
