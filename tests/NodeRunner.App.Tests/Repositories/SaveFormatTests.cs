using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using NodeRunner.App.Repositories;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Repositories;

/// <summary>
/// Guards docs/SAVE_FORMAT.md. A failing schema test means the saved shape changed: update the doc
/// and docs/save-schema/ in the same change (the test writes the new schema to the test output
/// folder). The example file checks that a real file loads.
/// </summary>
public sealed class SaveFormatTests : IDisposable
{
    private static readonly Guid _exampleId = Guid.Parse("0f3c6a52-7d1e-4b8a-9c2f-5e6d7a8b9c0d");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"node-runner-save-format-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(typeof(CreationDef), "creation.schema.json")]
    [InlineData(typeof(ProgressionDef), "progression.schema.json")]
    public void Schema_MatchesTheCommittedSchema(Type type, string name)
    {
        var generated = JsonSchemaExporter
            .GetJsonSchemaAsNode(SaveJson.Options, type, new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true })
            .ToJsonString(SaveJson.Options);
        var committedPath = Path.Combine(AppContext.BaseDirectory, "SaveSchema", name);
        if (Normalize(generated) == Normalize(File.ReadAllText(committedPath)))
        {
            return;
        }

        var actualPath = $"{committedPath}.actual";
        File.WriteAllText(actualPath, generated + "\n");
        Assert.Fail($"The saved shape behind {name} changed. Update docs/SAVE_FORMAT.md and docs/save-schema/{name} (new schema: {actualPath}).");
    }

    [Fact]
    public void Example_Loads()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CreationPath(_exampleId))!);
        File.WriteAllText(CreationPath(_exampleId), Example());

        var loaded = new FileCreationRepository(new Location(_directory)).Get(_exampleId);

        loaded.ShouldNotBeNull();
        SaveJson.Serialize(loaded).ShouldBe(SaveJson.Serialize(ExampleCreation()));
    }

    [Theory]
    [InlineData("\"name\": \"Example\",", "\"name\": \"Example\", \"isLocked\": true,", "isLocked")]
    [InlineData("\"radius\": 1,", "\"radius\": 1, \"mass\": 2,", "mass")]
    public void Loading_WithAnUnknownField_FailsAndNamesIt(string field, string withExtra, string extra)
    {
        var json = Example();
        json.ShouldContain(field);

        var error = Should.Throw<JsonException>(() => SaveJson.Deserialize<CreationDef>(ReplaceFirst(json, field, withExtra), "creation.json"));

        error.Message.ShouldContain(extra);
    }

    [Theory]
    [InlineData("kind")]
    [InlineData("generation")]
    [InlineData("bestFitness")]
    [InlineData("nextPartId")]
    [InlineData("x")]
    public void Loading_WithARequiredFieldMissing_FailsAndNamesIt(string field)
    {
        var json = JsonNode.Parse(Example())!;
        RemoveFirst(json, field).ShouldBeTrue();

        var error = Should.Throw<JsonException>(() => SaveJson.Deserialize<CreationDef>(json.ToJsonString(), "creation.json"));

        error.Message.ShouldContain(field);
    }

    [Fact]
    public void Loading_WithNullBrainShape_FailsAndNamesIt()
    {
        var json = JsonNode.Parse(Example())!.AsObject();
        json["brainShape"] = null;

        Should.Throw<JsonException>(() => SaveJson.Deserialize<CreationDef>(json.ToJsonString(), "creation.json"))
            .Message.ShouldContain("brainShape");
    }

    [Fact]
    public void Loading_WithNullBestRun_FailsAndNamesIt()
    {
        var json = JsonNode.Parse(Example())!.AsObject();
        json["training"]!["bestRun"] = null;

        Should.Throw<JsonException>(() => SaveJson.Deserialize<CreationDef>(json.ToJsonString(), "creation.json"))
            .Message.ShouldContain("bestRun");
    }

    [Fact]
    public void List_SkipsAFileWithANullPart()
    {
        var json = JsonNode.Parse(Example())!;
        json["creature"]!["nodes"]!.AsArray().Add(null);
        Directory.CreateDirectory(Path.GetDirectoryName(CreationPath(_exampleId))!);
        File.WriteAllText(CreationPath(_exampleId), json.ToJsonString());

        new FileCreationRepository(new Location(_directory)).List().ShouldBeEmpty();
    }

    [Fact]
    public void Repository_KeepsEachCreationInItsOwnFolder()
    {
        var repository = new FileCreationRepository(new Location(_directory));
        var creation = ExampleCreation();

        repository.Save(creation);
        File.Exists(CreationPath(creation.Id)).ShouldBeTrue();
        Directory.EnumerateFileSystemEntries(_directory).ShouldBe([Path.GetDirectoryName(CreationPath(creation.Id))!]);

        repository.Delete(creation.Id).ShouldBeTrue();
        Directory.EnumerateFileSystemEntries(_directory).ShouldBeEmpty();
    }

    private static CreationDef ExampleCreation() =>
        new(
            _exampleId,
            "Example",
            new CreatureDef(
                [new NodeDef(1, new Vector2D(0, 0), 1, "Hip"), new NodeDef(2, new Vector2D(2, 0.5), 1), new NodeDef(3, new Vector2D(4, 0), 1)],
                [new BeamDef(4, 1, 2, "Thigh"), new BeamDef(5, 2, 3)],
                [new SensorDef(6, 4, SensorKind.Accelerometer), new SensorDef(7, 5, SensorKind.Camera, "Eye", aim: -0.5)],
                nextPartId: 9),
            new BrainShapeDef(1, 2),
            new TrainingStateDef([2, 2, 1], [0.5, -0.25, 0.125, 1, -1, 0.75, 0.25, -0.5, 0], 12, "Tanh", 3.5, new TrainingRunDef(3.5, 1.25, 0.5, MapIds.Flat)));

    private static string Example() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Repositories", "SaveExamples", "creation.json"));

    private static bool RemoveFirst(JsonNode? node, string field) => node switch
    {
        JsonObject obj when obj.Remove(field) => true,
        JsonObject obj => obj.Any(property => RemoveFirst(property.Value, field)),
        JsonArray array => array.Any(item => RemoveFirst(item, field)),
        _ => false,
    };

    private static string Normalize(string json) => json.ReplaceLineEndings("\n").TrimEnd();

    private static string ReplaceFirst(string text, string value, string replacement)
    {
        var index = text.IndexOf(value, StringComparison.Ordinal);
        return text[..index] + replacement + text[(index + value.Length)..];
    }

    private string CreationPath(Guid id) => Path.Combine(_directory, id.ToString("N"), FileCreationRepository.CreationFileName);

    private sealed class Location(string directoryPath) : IStorageLocation
    {
        public string DirectoryPath { get; } = directoryPath;
    }
}
