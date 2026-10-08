using System.Text.Json;
using System.Text.Json.Nodes;

namespace NodeRunner.App.Repositories;

/// <summary>
/// Changes a file one version up. It edits the JSON in place, so it can rename, move or fill in a
/// field before the records load it. It throws <see cref="InvalidDataException"/> for a file it
/// can't change, which then fails to load; any other exception is a bug in the migration.
/// </summary>
public delegate void SaveMigration(JsonObject file);

/// <summary>The loaded value, and whether the file was older and should be written back.</summary>
public sealed record VersionedLoad<T>(T Value, bool IsOutdated);

/// <summary>
/// A save file with a format version (docs/SAVE_FORMAT.md → "Versions and migration"). Writing puts
/// <see cref="VersionField"/> first. Loading reads it, runs the migrations from that version up to
/// <see cref="CurrentVersion"/> in order, then loads the result strictly through <see cref="SaveJson"/>.
/// </summary>
public sealed class VersionedSaveFile<T>
    where T : class
{
    public const string VersionField = "formatVersion";

    /// <summary>The 0.13.0 shape. A file without <see cref="VersionField"/> is this version.</summary>
    public const int BaselineVersion = 1;

    private readonly IReadOnlyList<SaveMigration> _migrations;

    /// <param name="migrations">In order: the first one changes a version-1 file to version 2.</param>
    public VersionedSaveFile(IReadOnlyList<SaveMigration> migrations)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        _migrations = migrations.ToArray();
    }

    public int CurrentVersion => BaselineVersion + _migrations.Count;

    public string Serialize(T value)
    {
        var file = JsonSerializer.SerializeToNode(value, SaveJson.Options)!.AsObject();
        file.Insert(0, VersionField, CurrentVersion);
        return file.ToJsonString(SaveJson.Options);
    }

    /// <exception cref="JsonException">The file is not JSON, or fails strict loading.</exception>
    /// <exception cref="InvalidDataException">The file's version is not one this app can read.</exception>
    public VersionedLoad<T> Deserialize(string json, string path)
    {
        var file = JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidDataException($"Save file '{path}' is not a JSON object.");
        var version = ReadVersion(file, path, out var hasVersion);
        file.Remove(VersionField);

        for (var from = version; from < CurrentVersion; from++)
        {
            _migrations[from - BaselineVersion](file);
        }

        var value = file.Deserialize<T>(SaveJson.Options)
            ?? throw new InvalidDataException($"Save file '{path}' is empty or invalid.");
        return new VersionedLoad<T>(value, !hasVersion || version < CurrentVersion);
    }

    /// <summary>Whether <paramref name="json"/> is a file in a version newer than this app reads, so a newer app made it.</summary>
    /// <exception cref="JsonException">The file is not JSON.</exception>
    public bool IsNewer(string json) =>
        JsonNode.Parse(json) is JsonObject file
        && file[VersionField] is JsonValue value
        && value.TryGetValue<int>(out var version)
        && version > CurrentVersion;

    // A file without a version is the baseline. It is outdated even while the baseline is current,
    // so writing it back adds the field.
    private int ReadVersion(JsonObject file, string path, out bool hasVersion)
    {
        hasVersion = file.TryGetPropertyValue(VersionField, out var node);
        if (!hasVersion)
        {
            return BaselineVersion;
        }

        if (node is not JsonValue value || !value.TryGetValue<int>(out var version))
        {
            throw new InvalidDataException($"Save file '{path}' has a '{VersionField}' that is not a whole number.");
        }

        if (version < BaselineVersion || version > CurrentVersion)
        {
            throw new InvalidDataException(
                $"Save file '{path}' is format version {version}; this app reads {BaselineVersion} to {CurrentVersion}.");
        }

        return version;
    }
}
