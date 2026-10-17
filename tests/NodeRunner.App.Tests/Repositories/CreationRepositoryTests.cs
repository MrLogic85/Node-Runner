using NodeRunner.App.Builders;
using NodeRunner.App.Repositories;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Repositories;

public sealed class CreationRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"node-runner-repository-{Guid.NewGuid():N}");

    private string ProgressionPath => Path.Combine(_directory, "progression.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void InMemory_SavesByCreationIdentity()
    {
        var repository = new InMemoryCreationRepository();
        var original = CreateCreation("Alpha");
        var replacement = new CreationDef(original.Id, "Renamed", original.Creature, original.Training);

        repository.Save(original);
        repository.Save(replacement);

        repository.List().ShouldBe([replacement]);
        repository.Get(original.Id).ShouldBe(replacement);
        repository.Delete(original.Id).ShouldBeTrue();
        repository.Get(original.Id).ShouldBeNull();
    }

    [Fact]
    public void File_SaveListGetAndDelete_RoundTripTheCreation_WithNamesAndRetiredIds()
    {
        var repository = new FileCreationRepository(new TestStorageLocation(_directory));
        repository.List().ShouldBeEmpty();
        var builder = new CreatureBuilder();
        var left = builder.AddNode(new Vector2D(0, 0));
        var beam = builder.AddBeam(left, builder.AddNode(new Vector2D(2, 0)));
        var extra = builder.AddNode(new Vector2D(4, 0));
        builder.Rename(left, "Hip");
        builder.Rename(beam, "Thigh");
        builder.RemoveNode(extra);
        var named = new CreationDef(Guid.NewGuid(), "Named", builder.Build(), null);
        var creation = CreateCreation("Persistent");

        repository.Save(creation);
        repository.Save(named);

        var listed = repository.List();
        listed.Select(saved => saved.Name).ShouldBe(["Named", "Persistent"]);
        AssertEquivalent(listed[1], creation);
        AssertEquivalent(repository.Get(creation.Id), creation);
        var reloaded = repository.Get(named.Id)!.Creature;
        reloaded.Nodes[reloaded.NodeIndexOf(left)].Name.ShouldBe("Hip");
        reloaded.Beams[reloaded.BeamIndexOf(beam)].Name.ShouldBe("Thigh");
        reloaded.NextPartId.ShouldBe(extra + 1);
        repository.Delete(named.Id).ShouldBeTrue();
        repository.Delete(named.Id).ShouldBeFalse();
        repository.Get(named.Id).ShouldBeNull();
    }

    // #114: one corrupt file must not empty the Creations list, nor throw out of Get for the open Creation.
    [Fact]
    public void File_ACorruptCreationFile_IsSkippedByListAndNullFromGet()
    {
        var repository = new FileCreationRepository(new TestStorageLocation(_directory));
        var healthy = CreateCreation("Healthy");
        repository.Save(healthy);
        var corruptId = Guid.NewGuid();
        var corruptFolder = Path.Combine(_directory, corruptId.ToString("N"));
        Directory.CreateDirectory(corruptFolder);
        File.WriteAllText(Path.Combine(corruptFolder, FileCreationRepository.CreationFileName), "{ this is not valid json");

        var listed = repository.List();

        listed.Select(creation => creation.Id).ShouldBe([healthy.Id]);
        repository.Get(corruptId).ShouldBeNull();
    }

    // #113 and #114: overlapping writers, from one instance or several, must never leave a corrupt,
    // missing or quarantined file.
    [Fact]
    public void File_ConcurrentSavesAndLoads_NeverLeaveACorruptOrMissingFile()
    {
        var repository = new FileCreationRepository(new TestStorageLocation(_directory));
        var creation = CreateCreation("Concurrent");
        var storageLocation = new TestStorageLocation(_directory);
        new FileProgressionRepository(storageLocation).Save(new ProgressionDef(DefaultCreationsSeeded: true));

        Parallel.For(0, 16, i =>
        {
            repository.Save(new CreationDef(creation.Id, creation.Name, creation.Creature, TestTraining.State(i + 1, 1, TestTraining.Run)));

            // A fresh instance per iteration exercises the cross-instance path.
            var progression = new FileProgressionRepository(storageLocation);
            progression.Save(new ProgressionDef(DefaultCreationsSeeded: true));
            progression.Load();
        });

        repository.Get(creation.Id).ShouldNotBeNull().Training!.Generation.ShouldBeInRange(1, 16);
        Directory.EnumerateFiles(_directory, "progression.json.corrupt-*").ShouldBeEmpty();
        new FileProgressionRepository(storageLocation).Load().DefaultCreationsSeeded.ShouldBeTrue();
    }

    // #114: a missing file is a fresh progression; a corrupt one, including "{}" that the constructor's
    // defaults would read as fresh, is quarantined and reset rather than crashing.
    [Theory]
    [InlineData(null)]
    [InlineData("""{ "removedFlag": true, "defaultCreationsSeeded": true }""")]
    [InlineData("{ not valid json")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void FileProgression_LoadOfAMissingOrCorruptFile_StartsFresh(string? json)
    {
        if (json is not null)
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(ProgressionPath, json);
        }

        new FileProgressionRepository(new TestStorageLocation(_directory)).Load().ShouldBe(new ProgressionDef());

        File.Exists(ProgressionPath).ShouldBeFalse();
        Directory.Exists(_directory).ShouldBe(json is not null);
        if (json is not null)
        {
            Directory.EnumerateFiles(_directory, "progression.json.corrupt-*").ShouldHaveSingleItem();
        }
    }

    private static CreationDef CreateCreation(string name) =>
        new(
            Guid.NewGuid(),
            name,
            new CreatureDef(
                [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0)), new NodeDef(3, new Vector2D(4, 0))],
                [new BeamDef(101, 1, 2), new BeamDef(102, 2, 3)],
                [new SensorDef(201, 101, SensorKind.Accelerometer), new SensorDef(202, 102, SensorKind.Camera)]),
            new TrainingStateDef(
                TestTraining.Network,
                5,
                new TrainingRunDef(40.25, 88.25, 12, MapIds.Flat),
                new TrainingBestDef(2, 42.5, MapIds.Flat)));

    private static void AssertEquivalent(CreationDef? actual, CreationDef expected)
    {
        actual.ShouldNotBeNull();
        actual.Id.ShouldBe(expected.Id);
        actual.Name.ShouldBe(expected.Name);
        actual.Creature.Parts.ShouldBe(expected.Creature.Parts);
        actual.Training.ShouldNotBeNull();
        actual.Training.Network.Neurons.ShouldBe(expected.Training!.Network.Neurons);
        actual.Training.Network.Connections.ShouldBe(expected.Training.Network.Connections);
        actual.Training.Network.NextNeuronId.ShouldBe(expected.Training.Network.NextNeuronId);
        actual.Training.Generation.ShouldBe(expected.Training.Generation);
        actual.Training.Latest.ShouldBe(expected.Training.Latest);
        actual.Training.Best.ShouldBe(expected.Training.Best);
    }
}
