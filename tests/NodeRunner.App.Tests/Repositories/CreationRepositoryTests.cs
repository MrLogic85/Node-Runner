using NodeRunner.App.Repositories;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Repositories;

public sealed class CreationRepositoryTests
{
    [Fact]
    public void InMemory_SaveListAndDelete_UsesCreationIdentity()
    {
        var repository = new InMemoryCreationRepository();
        var creation = CreateCreation("Alpha");

        repository.Save(creation);

        repository.Get(creation.Id).ShouldBe(creation);
        repository.List().ShouldContain(creation);
        repository.Delete(creation.Id).ShouldBeTrue();
        repository.Get(creation.Id).ShouldBeNull();
    }

    [Fact]
    public void InMemory_SaveSameId_ReplacesExistingCreation()
    {
        var repository = new InMemoryCreationRepository();
        var original = CreateCreation("Alpha");
        var replacement = new CreationDef(original.Id, "Renamed", original.Creature, original.Training);

        repository.Save(original);
        repository.Save(replacement);

        repository.List().ShouldBe([replacement]);
    }

    [Fact]
    public void InMemory_DuplicatePattern_CanCopyTrainingStateWithNewIdentity()
    {
        var repository = new InMemoryCreationRepository();
        var original = CreateCreation("Alpha");
        var copy = new CreationDef(Guid.NewGuid(), "Alpha Copy", original.Creature, original.Training);

        repository.Save(original);
        repository.Save(copy);

        repository.List().Count.ShouldBe(2);
        copy.Training.ShouldBe(original.Training);
        copy.Id.ShouldNotBe(original.Id);
    }

    [Fact]
    public void File_SaveAndList_RoundTripsCreation()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"node-runner-{Guid.NewGuid():N}");
        try
        {
            var repository = new FileCreationRepository(new TestStorageLocation(directory));
            var creation = CreateCreation("Persistent");

            repository.Save(creation);

            var listed = repository.List();
            listed.Count.ShouldBe(1);
            AssertEquivalent(listed[0], creation);
            AssertEquivalent(repository.Get(creation.Id), creation);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void File_DeleteMissingCreation_ReturnsFalse()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"node-runner-{Guid.NewGuid():N}");
        var repository = new FileCreationRepository(new TestStorageLocation(directory));

        repository.Delete(Guid.NewGuid()).ShouldBeFalse();
    }

    private static CreationDef CreateCreation(string name)
    {
        return new CreationDef(
            Guid.NewGuid(),
            name,
            new CreatureDef(
                [new NodeDef(new Vector2D(0, 0), 1), new NodeDef(new Vector2D(2, 0), 1)],
                [new BeamDef(0, 1)],
                [new CoreDef(0)]),
            new TrainingStateDef([2, 1], [0.1, -0.2, 0.3], 2, "Tanh"));
    }

    private static void AssertEquivalent(CreationDef? actual, CreationDef expected)
    {
        actual.ShouldNotBeNull();
        actual.Id.ShouldBe(expected.Id);
        actual.Name.ShouldBe(expected.Name);
        actual.Creature.Nodes.ToArray().ShouldBe(expected.Creature.Nodes.ToArray());
        actual.Creature.Beams.ToArray().ShouldBe(expected.Creature.Beams.ToArray());
        actual.Creature.Cores.ToArray().ShouldBe(expected.Creature.Cores.ToArray());
        actual.Training.ShouldNotBeNull();
        actual.Training.LayerSizes.ShouldBe(expected.Training!.LayerSizes);
        actual.Training.BestGenome.ShouldBe(expected.Training.BestGenome);
        actual.Training.Generation.ShouldBe(expected.Training.Generation);
        actual.Training.Activation.ShouldBe(expected.Training.Activation);
    }

    private sealed class TestStorageLocation(string directoryPath) : IStorageLocation
    {
        public string DirectoryPath { get; } = directoryPath;
    }
}
