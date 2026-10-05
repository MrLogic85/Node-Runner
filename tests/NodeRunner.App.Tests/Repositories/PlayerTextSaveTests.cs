using NodeRunner.App.Repositories;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Repositories;

/// <summary>
/// Player-typed names (the Creation's and each part's) must never break a save (#837): whatever
/// a name holds, the file is written and reads back to the same Creation.
/// </summary>
public sealed class PlayerTextSaveTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"node-runner-player-text-{Guid.NewGuid():N}");

    public static TheoryData<string> HostileNames() =>
    [
        "\"quoted\" and \\back\\slashed",
        "{\"name\": null}, \"nodes\": []",
        "line\nbreak\r\nand\ttab",
        "nul\0and\u0001control\u001f",
        "../../../etc/passwd",
        "C:\\Windows\\..\\con",
        "</script><b>&amp;'",
        "emoji 🦖🏃‍♀️👍🏽",
        "åäö ÅÄÖ ß 中文 العربية עברית",
        "zero\u200Bwidth\u200D\uFEFFbom",
        "\u2028line\u2029separators",
        "\uFFFF\uFFFE noncharacters",
        new string('x', 100_000),
    ];

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Theory]
    [MemberData(nameof(HostileNames))]
    public void File_SaveAndGet_RoundTripsAnyPlayerName(string name)
    {
        var repository = new FileCreationRepository(new TestStorageLocation(_directory));
        var creation = CreationNamed(name);

        repository.Save(creation);

        var loaded = new FileCreationRepository(new TestStorageLocation(_directory)).Get(creation.Id);
        loaded.ShouldNotBeNull();
        loaded.Name.ShouldBe(name);
        loaded.Creature.Nodes[0].Name.ShouldBe(name);
        loaded.Creature.Beams[0].Name.ShouldBe(name);
        loaded.Creature.Sensors[0].Name.ShouldBe(name);
        loaded.Creature.Pistons[0].Name.ShouldBe(name);
        loaded.Creature.Springs[0].Name.ShouldBe(name);
    }

    // System.Text.Json writes half of a surrogate pair as U+FFFD; the file still loads. The names
    // are built here, not passed as theory data: xUnit stores theory strings as UTF-8, which
    // would replace the half pair before the test runs.
    [Fact]
    public void File_SaveAndGet_ReplacesALoneSurrogate()
    {
        var repository = new FileCreationRepository(new TestStorageLocation(_directory));
        foreach (var half in new[] { '\uD83D', '\uDE00' })
        {
            var creation = CreationNamed($"lone {half} surrogate");

            repository.Save(creation);

            var loaded = new FileCreationRepository(new TestStorageLocation(_directory)).Get(creation.Id);
            loaded.ShouldNotBeNull();
            loaded.Name.ShouldBe("lone \uFFFD surrogate");
            loaded.Creature.Nodes[0].Name.ShouldBe("lone \uFFFD surrogate");
        }
    }

    private static CreationDef CreationNamed(string name) =>
        new(
            Guid.NewGuid(),
            name,
            new CreatureDef(
                [new NodeDef(1, new Vector2D(0, 0), name), new NodeDef(2, new Vector2D(2, 0)), new NodeDef(3, new Vector2D(4, 0))],
                [new BeamDef(4, 1, 2, name)],
                [new SensorDef(5, 4, SensorKind.Accelerometer, name)],
                [new PistonDef(6, 1, 3, name)],
                [new SpringDef(7, 2, 3, name)],
                nextPartId: 8));
}
