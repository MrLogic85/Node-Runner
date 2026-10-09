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

    // The stroke a version 2 save meant when it left the field out.
    private const double _strokeBefore870 = 0.3;

    /// <summary>
    /// <c>creation.json</c>'s versions. Add a migration here when its shape changes
    /// (docs/SAVE_FORMAT.md → "Versions and migration").
    /// </summary>
    public static VersionedSaveFile<CreationDef> Format { get; } = new([AddServosArray, PistonStrokeFromShortest, SpringTravel, AddWheelsArray]);

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

    // Before #870 a Piston's stroke was ±s of its built length. Now it grows by stroke of its
    // shortest length, and start says where the built length sits in that travel: 2s / (1 − s) at
    // start 0.5 kept the same shortest and longest lengths, until #835 measured its travel on the gap
    // between its joints' edges, which shortens it a little. A Piston can now at most double, so
    // a stroke above ±⅓ becomes 100%, about ±33% of its built length.
    private static void PistonStrokeFromShortest(JsonObject file)
    {
        PistonStrokesFromShortest(file);
        PistonLengthInputsFromShortest(file);
    }

    private static void PistonStrokesFromShortest(JsonObject file)
    {
        if (file["creature"]?["pistons"] is not JsonArray pistons)
        {
            throw new InvalidDataException("creation.json is missing its creature's pistons.");
        }

        foreach (var node in pistons)
        {
            if (node is not JsonObject piston)
            {
                throw new InvalidDataException("creation.json has a piston that is not an object.");
            }

            var oldStroke = piston["stroke"] switch
            {
                null => _strokeBefore870,
                JsonValue value when value.TryGetValue<double>(out var stroke) => stroke,
                _ => throw new InvalidDataException("creation.json has a piston stroke that is not a number."),
            };
            if (!double.IsFinite(oldStroke) || oldStroke <= 0 || oldStroke >= 1)
            {
                throw new InvalidDataException($"creation.json has a piston stroke of {oldStroke}, outside 0…1.");
            }

            piston["stroke"] = Math.Min(2 * oldStroke / (1 - oldStroke), 1);
            piston["start"] = 0.5;
        }
    }

    // A Piston's length input was −1…1 around its built length and is now 0…1 over its travel, so
    // old = 2·new − 1. Doubling each weight from it and taking the old weight off the bias it feeds
    // keeps a trained brain doing what it did, exactly while its stroke was within ±⅓.
    private static void PistonLengthInputsFromShortest(JsonObject file)
    {
        if (file["training"]?["brain"] is not JsonObject brain)
        {
            return;
        }

        if (brain["neurons"] is not JsonArray neurons || brain["connections"] is not JsonArray connections)
        {
            throw new InvalidDataException("creation.json has a brain without neurons or connections.");
        }

        var pistonIds = file["creature"]!["pistons"]!.AsArray()
            .Select(piston => IntOf(piston!["id"], "piston id"))
            .ToHashSet();
        var neuronsById = neurons
            .Select(neuron => neuron as JsonObject ?? throw new InvalidDataException("creation.json has a neuron that is not an object."))
            .ToDictionary(neuron => IntOf(neuron["id"], "neuron id"));
        var lengthInputs = neuronsById
            .Where(entry => TextOf(entry.Value["kind"]) == "input"
                && TextOf(entry.Value["channel"]) == BrainPorts.PistonLengthChannel
                && entry.Value["partId"] is { } partId
                && pistonIds.Contains(IntOf(partId, "neuron partId")))
            .Select(entry => entry.Key)
            .ToHashSet();

        foreach (var node in connections)
        {
            if (node is not JsonObject connection)
            {
                throw new InvalidDataException("creation.json has a connection that is not an object.");
            }

            if (!lengthInputs.Contains(IntOf(connection["from"], "connection from")))
            {
                continue;
            }

            var weight = NumberOf(connection["weight"], "connection weight");
            connection["weight"] = 2 * weight;
            if (connection["enabled"] is not JsonValue enabled || !enabled.TryGetValue<bool>(out var isEnabled) || isEnabled)
            {
                var target = neuronsById[IntOf(connection["to"], "connection to")];
                target["bias"] = NumberOf(target["bias"], "neuron bias") - weight;
            }
        }
    }

    // Before #835 a Spring had no travel: it pushed nothing at its drawn length and nothing stopped
    // it. Stroke 1 and coil length 0.55 rest it at its drawn length, between stops about a third of the
    // gap between its joints' edges either side (#974), so it stays free there for every move but the largest.
    private static void SpringTravel(JsonObject file)
    {
        if (file["creature"] is not JsonObject creature)
        {
            throw new InvalidDataException("creation.json is missing its creature object.");
        }

        // A file without springs fails the strict load, which names the missing field.
        if (creature["springs"] is not JsonArray springs)
        {
            return;
        }

        foreach (var node in springs)
        {
            if (node is not JsonObject spring)
            {
                throw new InvalidDataException("creation.json has a spring that is not an object.");
            }

            spring["stroke"] = 1.0;
            spring["coilLength"] = 0.55;
        }
    }

    // Before #129 a creature had no Wheels.
    private static void AddWheelsArray(JsonObject file)
    {
        if (file["creature"] is not JsonObject creature)
        {
            throw new InvalidDataException("creation.json is missing its creature object.");
        }

        creature["wheels"] ??= new JsonArray();
    }

    private static string? TextOf(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static int IntOf(JsonNode? node, string what) =>
        node is JsonValue value && value.TryGetValue<int>(out var number)
            ? number
            : throw new InvalidDataException($"creation.json has a {what} that is not a whole number.");

    private static double NumberOf(JsonNode? node, string what) =>
        node is JsonValue value && value.TryGetValue<double>(out var number)
            ? number
            : throw new InvalidDataException($"creation.json has a {what} that is not a number.");

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
