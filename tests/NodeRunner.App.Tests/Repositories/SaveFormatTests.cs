using System.Text.Json;
using System.Text.Json.Nodes;
using NodeRunner.App.Repositories;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Repositories;

/// <summary>
/// Guards docs/SAVE_FORMAT.md. A failing golden test means the saved shape changed: update the doc
/// and the golden file in the same change (the test writes the new shape next to the golden file
/// in the test output folder).
/// </summary>
public sealed class SaveFormatTests : IDisposable
{
    private static readonly Guid _goldenId = Guid.Parse("0f3c6a52-7d1e-4b8a-9c2f-5e6d7a8b9c0d");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"node-runner-save-format-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void CreationFile_MatchesTheGoldenFile()
    {
        new FileCreationRepository(new Location(_directory)).Save(GoldenCreation());

        ShouldMatchGolden(File.ReadAllText(CreationPath(_goldenId)), "creation.json");
    }

    [Fact]
    public void CreationFile_GoldenLoadsAndSavesUnchanged()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CreationPath(_goldenId))!);
        File.WriteAllText(CreationPath(_goldenId), Golden("creation.json"));
        var repository = new FileCreationRepository(new Location(_directory));

        var loaded = repository.Get(_goldenId);
        loaded.ShouldNotBeNull();
        repository.Save(loaded);

        Normalize(File.ReadAllText(CreationPath(_goldenId))).ShouldBe(Normalize(Golden("creation.json")));
    }

    [Fact]
    public void ProgressionFile_MatchesTheGoldenFile()
    {
        new FileProgressionRepository(new Location(_directory)).Save(new ProgressionDef(DefaultCreationsSeeded: true));

        ShouldMatchGolden(File.ReadAllText(Path.Combine(_directory, "progression.json")), "progression.json");
    }

    [Fact]
    public void ProgressionFile_GoldenLoads()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "progression.json"), Golden("progression.json"));

        new FileProgressionRepository(new Location(_directory)).Load().ShouldBe(new ProgressionDef(DefaultCreationsSeeded: true));
    }

    [Theory]
    [InlineData("\"name\": \"Golden\",", "\"name\": \"Golden\", \"isLocked\": true,", "isLocked")]
    [InlineData("\"radius\": 1,", "\"radius\": 1, \"mass\": 2,", "mass")]
    public void Loading_WithAnUnknownField_FailsAndNamesIt(string field, string withExtra, string extra)
    {
        var json = Golden("creation.json");
        json.ShouldContain(field);

        var error = Should.Throw<JsonException>(() => SaveJson.Deserialize<CreationDef>(ReplaceFirst(json, field, withExtra), "creation.json"));

        error.Message.ShouldContain(extra);
    }

    [Fact]
    public void Loading_WithoutABrainShape_FailsAndNamesIt()
    {
        var json = JsonNode.Parse(Golden("creation.json"))!.AsObject();
        json["brainShape"] = null;

        Should.Throw<ArgumentNullException>(() => SaveJson.Deserialize<CreationDef>(json.ToJsonString(), "creation.json"))
            .ParamName.ShouldBe("brainShape");
    }

    [Fact]
    public void Loading_WithoutABestRun_FailsAndNamesIt()
    {
        var json = JsonNode.Parse(Golden("creation.json"))!.AsObject();
        json["training"]!["bestRun"] = null;

        Should.Throw<ArgumentNullException>(() => SaveJson.Deserialize<CreationDef>(json.ToJsonString(), "creation.json"))
            .ParamName.ShouldBe("bestRun");
    }

    [Fact]
    public void Repository_KeepsEachCreationInItsOwnFolder()
    {
        var repository = new FileCreationRepository(new Location(_directory));
        var creation = GoldenCreation();

        repository.Save(creation);
        File.Exists(CreationPath(creation.Id)).ShouldBeTrue();
        Directory.EnumerateFileSystemEntries(_directory).ShouldBe([Path.GetDirectoryName(CreationPath(creation.Id))!]);

        repository.Delete(creation.Id).ShouldBeTrue();
        Directory.EnumerateFileSystemEntries(_directory).ShouldBeEmpty();
    }

    private static CreationDef GoldenCreation() =>
        new(
            _goldenId,
            "Golden",
            new CreatureDef(
                [new NodeDef(1, new Vector2D(0, 0), 1, "Hip"), new NodeDef(2, new Vector2D(2, 0.5), 1), new NodeDef(3, new Vector2D(4, 0), 1)],
                [new BeamDef(4, 1, 2, "Thigh"), new BeamDef(5, 2, 3)],
                [new SensorDef(6, 4, SensorKind.Accelerometer), new SensorDef(7, 5, SensorKind.Camera, "Eye", aim: -0.5)],
                nextPartId: 9),
            new BrainShapeDef(1, 2),
            new TrainingStateDef([2, 2, 1], [0.5, -0.25, 0.125, 1, -1, 0.75, 0.25, -0.5, 0], 12, "Tanh", 3.5, new TrainingRunDef(3.5, 1.25, 0.5, MapIds.Flat)));

    private static void ShouldMatchGolden(string actual, string name)
    {
        if (Normalize(actual) == Normalize(Golden(name)))
        {
            return;
        }

        var actualPath = Path.Combine(AppContext.BaseDirectory, "Repositories", "Golden", $"{name}.actual");
        File.WriteAllText(actualPath, actual);
        Assert.Fail($"The saved shape of {name} changed. Update docs/SAVE_FORMAT.md and tests/NodeRunner.App.Tests/Repositories/Golden/{name} (new shape: {actualPath}).");
    }

    private static string Golden(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Repositories", "Golden", name));

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
