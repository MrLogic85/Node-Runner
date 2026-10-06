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
    [InlineData(typeof(CreationDef), "creation.schema.json", true)]
    [InlineData(typeof(ProgressionDef), "progression.schema.json", true)]
    public void Schema_MatchesTheCommittedSchema(Type type, string name, bool versioned)
    {
        var schema = JsonSchemaExporter
            .GetJsonSchemaAsNode(SaveJson.Options, type, new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true })
            .AsObject();
        if (versioned)
        {
            AddVersionField(schema);
        }

        var generated = schema.ToJsonString(SaveJson.Options);
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

        var loaded = new FileCreationRepository(new TestStorageLocation(_directory)).Get(_exampleId);

        loaded.ShouldNotBeNull();
        SaveJson.Serialize(loaded).ShouldBe(SaveJson.Serialize(ExampleCreation()));
    }

    [Theory]
    [InlineData("\"name\": \"Example\",", "\"name\": \"Example\", \"isLocked\": true,", "isLocked")]
    [InlineData("\"name\": \"Hip\"", "\"name\": \"Hip\", \"mass\": 2", "mass")]
    public void Loading_WithAnUnknownField_FailsAndNamesIt(string field, string withExtra, string extra)
    {
        var json = Example();
        json.ShouldContain(field);

        var error = Should.Throw<JsonException>(() => Load(ReplaceFirst(json, field, withExtra), "creation.json"));

        error.Message.ShouldContain(extra);
    }

    [Theory]
    [InlineData("kind")]
    [InlineData("generation")]
    [InlineData("latest")]
    [InlineData("best")]
    [InlineData("enabled")]
    [InlineData("nextNeuronId")]
    [InlineData("springs")]
    [InlineData("nextPartId")]
    [InlineData("x")]
    public void Loading_WithARequiredFieldMissing_FailsAndNamesIt(string field)
    {
        var json = JsonNode.Parse(Example())!;
        RemoveFirst(json, field).ShouldBeTrue();

        var error = Should.Throw<JsonException>(() => Load(json.ToJsonString(), "creation.json"));

        error.Message.ShouldContain(field);
    }

    [Fact]
    public void Loading_ASaveFromBeforeFrontDistance_LoadsWithoutIt()
    {
        var json = JsonNode.Parse(Example())!;
        RemoveFirst(json, "frontDistance").ShouldBeTrue();
        RemoveFirst(json, "frontDistance").ShouldBeTrue();

        var training = Load(json.ToJsonString(), "creation.json").Training!;

        training.Latest.FrontDistance.ShouldBeNull();
        training.Latest.ShownDistance.ShouldBe(3.5);
        training.Best.FrontDistance.ShouldBeNull();
    }

    [Fact]
    public void Loading_WithNullBrain_FailsAndNamesIt()
    {
        var json = JsonNode.Parse(Example())!.AsObject();
        json["training"]!["brain"] = null;

        Should.Throw<JsonException>(() => Load(json.ToJsonString(), "creation.json"))
            .Message.ShouldContain("brain");
    }

    [Fact]
    public void Loading_WithNullLatest_FailsAndNamesIt()
    {
        var json = JsonNode.Parse(Example())!.AsObject();
        json["training"]!["latest"] = null;

        Should.Throw<JsonException>(() => Load(json.ToJsonString(), "creation.json"))
            .Message.ShouldContain("latest");
    }

    [Fact]
    public void List_SkipsAFileWithANullPart()
    {
        var json = JsonNode.Parse(Example())!;
        json["creature"]!["nodes"]!.AsArray().Add(null);
        Directory.CreateDirectory(Path.GetDirectoryName(CreationPath(_exampleId))!);
        File.WriteAllText(CreationPath(_exampleId), json.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();
    }

    [Fact]
    public void Repository_KeepsEachCreationInItsOwnFolder()
    {
        var repository = new FileCreationRepository(new TestStorageLocation(_directory));
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
                [new NodeDef(1, new Vector2D(0, 0), "Hip"), new NodeDef(2, new Vector2D(2, 0.5)), new NodeDef(3, new Vector2D(4, 0)), new NodeDef(10, new Vector2D(2, -1.5))],
                [new BeamDef(4, 1, 2, "Thigh"), new BeamDef(5, 2, 3)],
                [new SensorDef(6, 4, SensorKind.Accelerometer), new SensorDef(7, 5, SensorKind.Camera, "Eye", aim: -0.5)],
                [new PistonDef(9, 1, 3, stroke: 2 * 0.3 / (1 - 0.3))],
                [new SpringDef(11, 2, 10, "Tail", stiffness: 300, damping: 0.25)],
                nextPartId: 12),
            new TrainingStateDef(ExampleBrain(), 12, new TrainingRunDef(3.5, 1.25, 0.5, MapIds.Flat, 3.75), new TrainingBestDef(9, 4.25, MapIds.Flat, 4.5)),
            new TrainSettingsDef(12, 20));

    // The direct brain for ExampleCreation's ports: the Accelerometer (6), the Camera (7) and the
    // Piston (9). Every input drives the Piston's position, one connection is disabled, and its
    // strength starts passive with no connection. The file is older than #870, so loading it doubles
    // the Piston length weight (0.5) and takes 0.5 off the position bias (0.25).
    private static BrainDef ExampleBrain()
    {
        (int Part, string Channel)[] inputs = [(6, "along"), (6, "across"), (7, "left1"), (7, "centre"), (7, "right1"), (9, BrainPorts.PistonLengthChannel), (9, BrainPorts.PistonSpeedChannel)];
        double[] weights = [0.125, 1, -1, 0.75, -0.5, 2 * 0.5, -0.25];
        var neurons = inputs
            .Select((input, index) => new NeuronDef(index + 1, NeuronKind.Input, input.Part, input.Channel, 0, 0, NeuronActivation.Identity))
            .Append(new NeuronDef(8, NeuronKind.Output, 9, BrainPorts.PistonPositionChannel, 1, 0.25 - 0.5, NeuronActivation.Tanh))
            .Append(new NeuronDef(9, NeuronKind.Output, 9, BrainPorts.PistonStrengthChannel, 1, -4, NeuronActivation.Sigmoid))
            .ToArray();
        var connections = weights
            .Select((weight, index) => new ConnectionGeneDef(index + 1, 8, weight, enabled: index != 2))
            .ToArray();
        return new BrainDef(neurons, connections, nextNeuronId: 10);
    }

    // VersionedSaveFile writes the version first, outside the record.
    private static void AddVersionField(JsonObject schema)
    {
        const string field = VersionedSaveFile<CreationDef>.VersionField;
        schema["properties"]!.AsObject().Insert(0, field, new JsonObject { ["type"] = "integer", ["minimum"] = VersionedSaveFile<CreationDef>.BaselineVersion });
        schema["required"]!.AsArray().Insert(0, field);
    }

    private static CreationDef Load(string json, string path) => FileCreationRepository.Format.Deserialize(json, path).Value;

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
}
