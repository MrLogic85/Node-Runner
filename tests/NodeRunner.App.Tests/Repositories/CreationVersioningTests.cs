using System.Text.Json.Nodes;
using NodeRunner.App.Repositories;
using NodeRunner.Domain;
using NodeRunner.ML.Brains;

namespace NodeRunner.App.Tests.Repositories;

/// <summary>
/// Guards docs/SAVE_FORMAT.md → "Versions and migration" for <c>creation.json</c>. The files in
/// SaveExamples/0.13.0/ were written by the 0.13.0 build and must keep loading; never edit them.
/// </summary>
public sealed class CreationVersioningTests : IDisposable
{
    private const string _versionField = VersionedSaveFile<CreationDef>.VersionField;

    private static readonly IEqualityComparer<CreationDef> _creationComparer =
        EqualityComparer<CreationDef>.Create((a, b) => SaveJson.Serialize(a) == SaveJson.Serialize(b), creation => creation.Id.GetHashCode());

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"node-runner-versioning-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("walker.creation.json")]
    [InlineData("trained-walker.creation.json")]
    public void Fixture013_LoadsAndIsWrittenBackInTheCurrentVersion(string fixture)
    {
        var original = Fixture013(fixture);
        var path = WriteCreation(original);

        var loaded = new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem();

        var written = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        written.First().Key.ShouldBe(_versionField);
        written[_versionField]!.GetValue<int>().ShouldBe(FileCreationRepository.Format.CurrentVersion);
        written.Remove(_versionField);
        var expected = JsonNode.Parse(original)!.AsObject();
        expected["creature"]!.AsObject()["servos"] = new JsonArray();
        foreach (var piston in expected["creature"]!["pistons"]!.AsArray())
        {
            piston!["stroke"] = 2 * 0.3 / (1 - 0.3);
            piston["start"] = 0.5;
        }

        // The brain's Piston length weights move too; Fixture013_ItsTrainedBrainDrivesItsPistonsAsBefore covers them.
        written["training"]?.AsObject().Remove("brain");
        expected["training"]?.AsObject().Remove("brain");
        JsonNode.DeepEquals(written, expected).ShouldBeTrue();
        new FileCreationRepository(new TestStorageLocation(_directory)).Get(loaded.Id).ShouldBe(loaded, _creationComparer);
    }

    // #870: ±s of the built length becomes 2s / (1 − s) of the shortest, from the middle; a Piston
    // can now at most double, so ±50% becomes 100%.
    [Theory]
    [InlineData(0.1, 2.0 / 9)]
    [InlineData(0.3, 6.0 / 7)]
    [InlineData(0.5, 1)]
    public void Fixture013_ItsPistonsKeepTheirShortestAndLongestLengths_UpToDoubling(double oldStroke, double stroke)
    {
        var file = JsonNode.Parse(Fixture013("walker.creation.json"))!.AsObject();
        file["creature"]!["pistons"]![0]!["stroke"] = oldStroke;
        file["creature"]!["pistons"]![1]!.AsObject().Remove("stroke");
        WriteCreation(file.ToJsonString());

        var pistons = new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem().Creature.Pistons;

        pistons[0].Stroke.ShouldBe(stroke, tolerance: 1e-12);
        pistons[0].Start.ShouldBe(0.5);
        pistons[1].Stroke.ShouldBe(6.0 / 7, tolerance: 1e-12);
        pistons[1].Start.ShouldBe(0.5);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("\"wide\"")]
    public void Fixture013_WithAPistonStrokeItCouldNotHaveSaved_IsSkipped(string oldStroke)
    {
        var file = JsonNode.Parse(Fixture013("walker.creation.json"))!.AsObject();
        file["creature"]!["pistons"]![0]!["stroke"] = JsonNode.Parse(oldStroke);
        WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();
    }

    // #870: a Piston's length input went from −1…1 around its built length to 0…1 over its travel,
    // so a migrated brain must give every output the same value for the same pose.
    [Fact]
    public void Fixture013_ItsTrainedBrainDrivesItsPistonsAsBefore()
    {
        var original = Fixture013("trained-walker.creation.json");
        var oldBrain = SaveJson.Deserialize<BrainDef>(JsonNode.Parse(original)!["training"]!["brain"]!.ToJsonString(), "brain");
        WriteCreation(original);
        var creation = new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem();
        var ports = BrainPorts.Of(creation.Creature);
        var before = DirectBrain.Network(oldBrain, ports);
        var after = DirectBrain.Network(creation.Training!.Brain, ports);
        var random = new Random(870);

        for (var pose = 0; pose < 20; pose++)
        {
            var oldInputs = ports.Inputs.Select(_ => (random.NextDouble() * 2) - 1).ToArray();
            var newInputs = ports.Inputs
                .Select((port, i) => port.Channel == BrainPorts.PistonLengthChannel ? (oldInputs[i] + 1) / 2 : oldInputs[i])
                .ToArray();

            after.Forward(newInputs).ShouldBe(before.Forward(oldInputs), tolerance: 1e-12);
        }
    }

    [Theory]
    [InlineData("neurons", "\"many\"")]
    [InlineData("connections", "[{ \"from\": \"three\", \"to\": 7, \"weight\": 1, \"enabled\": true }]")]
    [InlineData("connections", "[{ \"from\": 3, \"to\": 7, \"weight\": \"heavy\", \"enabled\": true }]")]
    public void Fixture013_WithABrainItCouldNotHaveSaved_IsSkipped(string field, string value)
    {
        var file = JsonNode.Parse(Fixture013("trained-walker.creation.json"))!.AsObject();
        file["training"]!["brain"]![field] = JsonNode.Parse(value);
        WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();
    }

    [Fact]
    public void Fixture013_KeepsItsTraining()
    {
        WriteCreation(Fixture013("trained-walker.creation.json"));

        var creation = new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem();

        creation.Training.ShouldNotBeNull();
        creation.Training.Generation.ShouldBeGreaterThan(1);
        creation.Training.Brain.Connections.ShouldNotBeEmpty();
    }

    [Fact]
    public void Save_WritesTheVersionFirst()
    {
        var repository = new FileCreationRepository(new TestStorageLocation(_directory));
        var creation = LoadFixture013("walker.creation.json");

        repository.Save(creation);

        var written = JsonNode.Parse(File.ReadAllText(CreationPath(creation.Id)))!.AsObject();
        written.First().Key.ShouldBe(_versionField);
        written[_versionField]!.GetValue<int>().ShouldBe(FileCreationRepository.Format.CurrentVersion);
    }

    [Fact]
    public void Load_InTheCurrentVersion_LeavesTheFileAlone()
    {
        var repository = new FileCreationRepository(new TestStorageLocation(_directory));
        var creation = LoadFixture013("walker.creation.json");
        repository.Save(creation);
        var path = CreationPath(creation.Id);
        var written = File.GetLastWriteTimeUtc(path);
        File.SetLastWriteTimeUtc(path, written.AddHours(-1));

        repository.Get(creation.Id).ShouldNotBeNull();

        File.GetLastWriteTimeUtc(path).ShouldBe(written.AddHours(-1));
    }

    [Fact]
    public void Repository_WritesAMigratedFileBackInTheNewVersion()
    {
        var format = new VersionedSaveFile<CreationDef>([file =>
        {
            RenameField("title", "name")(file);
            AddServosArray(file);
        }]);
        var file = JsonNode.Parse(Fixture013("walker.creation.json"))!.AsObject();
        file["title"] = file["name"]!.DeepClone();
        file.Remove("name");
        var path = WriteCreation(file.ToJsonString());

        var creation = new FileCreationRepository(new TestStorageLocation(_directory), format).List().ShouldHaveSingleItem();

        creation.Name.ShouldBe("Walker");
        var written = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        written[_versionField]!.GetValue<int>().ShouldBe(2);
        written.ContainsKey("title").ShouldBeFalse();
        written["name"]!.GetValue<string>().ShouldBe("Walker");
    }

    [Fact]
    public void Repository_SkipsAFileNewerThanTheApp_AndLeavesItUntouched()
    {
        var path = WriteCreation(WithVersion(Fixture013("walker.creation.json"), FileCreationRepository.Format.CurrentVersion + 1));
        var before = File.ReadAllBytes(path);

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();

        File.ReadAllBytes(path).ShouldBe(before);
    }

    [Fact]
    public void Repository_SkipsAFileWhoseMigrationFails_AndLeavesItUntouched()
    {
        var format = new VersionedSaveFile<CreationDef>([_ => throw new InvalidDataException("Cannot migrate.")]);
        var path = WriteCreation(Fixture013("walker.creation.json"));
        var before = File.ReadAllBytes(path);

        new FileCreationRepository(new TestStorageLocation(_directory), format).List().ShouldBeEmpty();

        File.ReadAllBytes(path).ShouldBe(before);
    }

    [Fact]
    public void Repository_SkipsAFileThatFailsTheStrictLoad_AndLeavesItUntouched()
    {
        var file = JsonNode.Parse(Fixture013("walker.creation.json"))!.AsObject();
        file["unknown"] = 1;
        var path = WriteCreation(file.ToJsonString());
        var before = File.ReadAllBytes(path);

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();

        File.ReadAllBytes(path).ShouldBe(before);
    }

    [Fact]
    public void WriteBack_WhenASaveReplacedTheFileAfterItWasRead_KeepsTheSave()
    {
        var path = CreationPath(Guid.Parse(JsonNode.Parse(Fixture013("walker.creation.json"))!["id"]!.GetValue<string>()));
        const string saved = "saved in the meantime";
        // The migration runs between the read and the write-back, so it stands in for a Save that lands there.
        var format = new VersionedSaveFile<CreationDef>([file =>
        {
            File.WriteAllText(path, saved);
            AddServosArray(file);
        }]);
        WriteCreation(Fixture013("walker.creation.json"));

        new FileCreationRepository(new TestStorageLocation(_directory), format).List().ShouldHaveSingleItem();

        File.ReadAllText(path).ShouldBe(saved);
    }

    [Fact]
    public void WriteBack_ThatFails_StillLoadsTheCreation()
    {
        var path = CreationPath(Guid.Parse(JsonNode.Parse(Fixture013("walker.creation.json"))!["id"]!.GetValue<string>()));
        var format = new VersionedSaveFile<CreationDef>([file =>
        {
            File.Delete(path);
            AddServosArray(file);
        }]);
        WriteCreation(Fixture013("walker.creation.json"));

        new FileCreationRepository(new TestStorageLocation(_directory), format).List().ShouldHaveSingleItem().Name.ShouldBe("Walker");
    }

    private static SaveMigration RenameField(string from, string to) => file =>
    {
        var value = file[from]!.DeepClone();
        file.Remove(from);
        file[to] = value;
    };

    private static void AddServosArray(JsonObject file) =>
        file["creature"]!.AsObject()["servos"] = new JsonArray();

    private static CreationDef LoadFixture013(string name) =>
        FileCreationRepository.Format.Deserialize(Fixture013(name), name).Value;

    private static string WithVersion(string json, int version)
    {
        var file = JsonNode.Parse(json)!.AsObject();
        file.Insert(0, _versionField, version);
        return file.ToJsonString();
    }

    private static string Fixture013(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Repositories", "SaveExamples", "0.13.0", name));

    private string WriteCreation(string json)
    {
        var id = Guid.Parse(JsonNode.Parse(json)!["id"]!.GetValue<string>());
        var path = CreationPath(id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
        return path;
    }

    private string CreationPath(Guid id) => Path.Combine(_directory, id.ToString("N"), FileCreationRepository.CreationFileName);
}
