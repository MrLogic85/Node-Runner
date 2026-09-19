using System.Text.Json;
using NodeRunner.Domain;

namespace NodeRunner.App.Repositories;

public sealed class FileProgressionRepository : IProgressionRepository
{
    private readonly string _path;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public FileProgressionRepository(IStorageLocation storageLocation)
    {
        ArgumentNullException.ThrowIfNull(storageLocation);
        _path = Path.Combine(storageLocation.DirectoryPath, "progression.json");
    }

    public ProgressionDef Load()
    {
        if (!File.Exists(_path))
        {
            return new ProgressionDef();
        }

        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<ProgressionDef>(json, _jsonOptions)
            ?? throw new InvalidDataException($"Progression file '{_path}' is empty or invalid.");
    }

    public void Save(ProgressionDef progression)
    {
        ArgumentNullException.ThrowIfNull(progression);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporaryPath = $"{_path}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(progression, _jsonOptions));
        File.Move(temporaryPath, _path, overwrite: true);
    }
}
