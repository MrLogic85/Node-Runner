using NodeRunner.App.Repositories;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Repositories;

/// <summary>
/// Player-typed names (the Creation's and each part's) must never break a save: whatever
/// a name holds, the file is written and reads back to the same Creation.
/// </summary>
public sealed class PlayerTextSaveTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"node-runner-player-text-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    // System.Text.Json writes half of a surrogate pair as U+FFFD; the file still loads.
    [Fact]
    public void File_SaveAndGet_RoundTripsAnyPlayerName()
    {
        var repository = new FileCreationRepository(new TestStorageLocation(_directory));
        (string Name, string Loaded)[] names =
        [
            .. new[]
            {
                "\"quoted\" and \\back\\slashed",
                "{\"name\": null}, \"nodes\": []",
                "line\nbreak\r\nand\ttab",
                "nul\0and\u0001control\u001f",
                "../../../etc/passwd",
                "</script><b>&amp;'",
                "emoji 🦖🏃‍♀️👍🏽 åäö 中文 العربية",
                "zero\u200Bwidth\u200D\uFEFFbom \u2028line\u2029separators \uFFFF\uFFFE",
                new string('x', 100_000),
            }.Select(name => (name, name)),
            ($"lone {'\uD83D'} surrogate", "lone \uFFFD surrogate"),
            ($"lone {'\uDE00'} surrogate", "lone \uFFFD surrogate"),
        ];

        foreach (var (name, loadedName) in names)
        {
            var creation = CreationNamed(name);
            repository.Save(creation);

            var loaded = new FileCreationRepository(new TestStorageLocation(_directory)).Get(creation.Id);

            loaded.ShouldNotBeNull();
            loaded.Name.ShouldBe(loadedName);
            loaded.Creature.Parts.Where(part => part.Name is not null).Select(part => part.Name).ShouldBe(Enumerable.Repeat(loadedName, 5));
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
