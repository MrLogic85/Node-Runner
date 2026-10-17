using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.Repositories;
using NodeRunner.Domain;
using NodeRunner.ML.Networks;

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
        var schema = JsonSchemaExporter
            .GetJsonSchemaAsNode(SaveJson.Options, type, new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true, TransformSchemaNode = OnlyLockedIsInfinite })
            .AsObject();
        AddVersionField(schema);

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

    // A file with a null part is skipped, never thrown out of List.
    [Fact]
    public void Example_Loads_AndAFileWithANullPartIsSkipped()
    {
        var withNullPart = JsonNode.Parse(Example())!;
        withNullPart["creature"]!["nodes"]!.AsArray().Add(null);
        WriteCreation(_exampleId, Example());
        WriteCreation(Guid.NewGuid(), withNullPart.ToJsonString());

        var listed = new FileCreationRepository(new TestStorageLocation(_directory)).List();

        SaveJson.Serialize(listed.ShouldHaveSingleItem()).ShouldBe(SaveJson.Serialize(ExampleCreation()));
    }

    // The example's Camera (7) was saved before its hit input, so its network has no hit neuron.
    // It loads unchanged and locked, and plays exactly as before whatever hit reads: the input
    // compiles silent. A refit, as an edit makes, adds the neuron, still silent.
    [Fact]
    public void Example_ACameraNetworkFromBeforeHit_PlaysAsBefore_AndARefitAddsHit()
    {
        var loaded = Load(Example());
        var network = loaded.Training!.Network;
        var ports = NetworkPorts.Of(loaded.Creature);
        var hit = NetworkPort.Input(7, "hit");
        var hitIndex = ports.Inputs.ToList().IndexOf(hit);
        network.Neurons.ShouldNotContain(neuron => neuron.PartId == 7 && neuron.Channel == "hit");
        CreationLock.IsLocked(loaded).ShouldBeTrue();

        double[] seenBefore = [0.3, -0.2, 0.1, 0.6, 0, 0.4, -0.5];
        double[] seenNow = [.. seenBefore[..hitIndex], 1, .. seenBefore[hitIndex..]];
        var before = DirectNetwork.Build(network, ports with { Inputs = ports.Inputs.Where(port => port != hit).ToArray() }).Forward(seenBefore);
        DirectNetwork.Build(network, ports).Forward(seenNow).ShouldBe(before);

        var refit = DirectNetwork.Refit(network, ports);
        refit.Neurons.ShouldContain(neuron => neuron.Kind == NeuronKind.Input && neuron.PartId == 7 && neuron.Channel == "hit");
        DirectNetwork.Build(refit, ports).Forward(seenNow).ShouldBe(before);
    }

    // Loading is strict: an unknown field, a required one missing or a null where a value belongs fails
    // and names the field.
    [Theory]
    [InlineData("unknown", "isLocked")]
    [InlineData("unknown", "mass")]
    [InlineData("missing", "x")]
    [InlineData("missing", "enabled")]
    [InlineData("missing", "springs")]
    [InlineData("null", "network")]
    [InlineData("null", "latest")]
    public void Loading_AFileOutsideTheShape_FailsAndNamesTheField(string change, string field)
    {
        var json = JsonNode.Parse(Example())!;
        switch (change)
        {
            case "unknown" when field == "isLocked":
                json["isLocked"] = true;
                break;
            case "unknown":
                json["creature"]!["nodes"]![0]![field] = 2;
                break;
            case "missing":
                RemoveFirst(json, field).ShouldBeTrue();
                break;
            default:
                json["training"]![field == "network" ? "brain" : field] = null;
                break;
        }

        Should.Throw<JsonException>(() => Load(json.ToJsonString())).Message.ShouldContain(field);
    }

    [Fact]
    public void Loading_ASaveFromBeforeFrontDistance_LoadsWithoutIt()
    {
        var json = JsonNode.Parse(Example())!;
        RemoveFirst(json, "frontDistance").ShouldBeTrue();
        RemoveFirst(json, "frontDistance").ShouldBeTrue();

        var training = Load(json.ToJsonString()).Training!;

        training.Latest.FrontDistance.ShouldBeNull();
        training.Latest.ShownDistance.ShouldBe(3.5);
        training.Best.FrontDistance.ShouldBeNull();
    }

    // Each Creation has its own folder, and a Camera's settings and a stroke of 200% survive the file.
    [Fact]
    public void Repository_KeepsEachCreationInItsOwnFolder_AndACamerasSettingsAndAMaxStroke()
    {
        var repository = new FileCreationRepository(new TestStorageLocation(_directory));
        var camera = new SensorDef(3, 2, SensorKind.Camera, "Eye", aim: -0.5, rays: 5, spread: Math.PI / 6, range: 350);
        var piston = new PistonDef(6, 1, 4, stroke: PistonDef.MaxStroke, start: 0);
        var spring = new SpringDef(7, 4, 5, stroke: SpringDef.MaxStroke, coilLength: 1);
        var creation = new CreationDef(
            Guid.NewGuid(),
            "Reacher",
            new CreatureDef(
                [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(4, new Vector2D(2, 0)), new NodeDef(5, new Vector2D(2, 2))],
                [new BeamDef(2, 1, 4)],
                [camera],
                [piston],
                [spring],
                nextPartId: 8));

        repository.Save(creation);

        Directory.EnumerateFileSystemEntries(_directory).ShouldBe([Path.GetDirectoryName(CreationPath(creation.Id))!]);
        File.Exists(CreationPath(creation.Id)).ShouldBeTrue();
        var loaded = repository.Get(creation.Id).ShouldNotBeNull().Creature;
        loaded.Parts.ShouldBe(creation.Creature.Parts);
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
                [new SpringDef(11, 2, 10, "Tail", stiffness: 300, damping: 0.25, stroke: 1, coilLength: 0.55)],
                nextPartId: 12),
            new TrainingStateDef(ExampleNetwork(), 12, new TrainingRunDef(3.5, 1.25, 0.5, MapIds.Flat, 3.75), new TrainingBestDef(9, 4.25, MapIds.Flat, 4.5)),
            new TrainSettingsDef(12, 20));

    // The direct network for ExampleCreation's ports: the Accelerometer (6), the Camera (7) and the
    // Piston (9). Every input drives the Piston's position, one connection is disabled, and its
    // strength starts passive with no connection. The file is older than version 3, so loading it doubles
    // the Piston length weight (0.5) and takes 0.5 off the position bias (0.25). It is older than
    // version 8 too, so the position output turns sigmoid with its bias and every weight into it doubled.
    private static NeuralNetworkDef ExampleNetwork()
    {
        (int Part, string Channel)[] inputs = [(6, "along"), (6, "across"), (7, "left1"), (7, "centre"), (7, "right1"), (9, NetworkPorts.PistonLengthChannel), (9, NetworkPorts.PistonSpeedChannel)];
        double[] weights = [2 * 0.125, 2 * 1, 2 * -1, 2 * 0.75, 2 * -0.5, 2 * 2 * 0.5, 2 * -0.25];
        var neurons = inputs
            .Select((input, index) => new NeuronDef(index + 1, NeuronKind.Input, input.Part, input.Channel, 0, 0, NeuronActivation.Identity))
            .Append(new NeuronDef(8, NeuronKind.Output, 9, NetworkPorts.PistonPositionChannel, 1, 2 * (0.25 - 0.5), NeuronActivation.Sigmoid))
            .Append(new NeuronDef(9, NeuronKind.Output, 9, NetworkPorts.PistonStrengthChannel, 1, -4, NeuronActivation.Sigmoid))
            .ToArray();
        var connections = weights
            .Select((weight, index) => new ConnectionGeneDef(index + 1, 8, weight, enabled: index != 2))
            .ToArray();
        return new NeuralNetworkDef(neurons, connections, nextNeuronId: 10);
    }

    // VersionedSaveFile writes the version first, outside the record.
    // A Brake's Strength is the one number saved as a named literal, and only "Infinity" when Locked;
    // BrakeDef rejects the others, so the schema allows a positive number or "Infinity".
    private static JsonNode OnlyLockedIsInfinite(JsonSchemaExporterContext context, JsonNode schema)
    {
        if (context.PropertyInfo is not { Name: "strength" } property || property.DeclaringType != typeof(BrakeDef))
        {
            return schema;
        }

        schema["anyOf"] = new JsonArray(
            new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
            new JsonObject { ["enum"] = new JsonArray("Infinity") });
        return schema;
    }

    private static void AddVersionField(JsonObject schema)
    {
        const string field = VersionedSaveFile<CreationDef>.VersionField;
        schema["properties"]!.AsObject().Insert(0, field, new JsonObject { ["type"] = "integer", ["minimum"] = VersionedSaveFile<CreationDef>.BaselineVersion });
        schema["required"]!.AsArray().Insert(0, field);
    }

    private static CreationDef Load(string json) => FileCreationRepository.Format.Deserialize(json, "creation.json").Value;

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

    private void WriteCreation(Guid id, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CreationPath(id))!);
        File.WriteAllText(CreationPath(id), json);
    }

    private string CreationPath(Guid id) => Path.Combine(_directory, id.ToString("N"), FileCreationRepository.CreationFileName);
}
