using NodeRunner.App.Builders;
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
    public void File_ConcurrentSaves_NeverLeaveACorruptOrMissingFile()
    {
        // Regression guard for #113: persistence now runs off the main
        // thread, so overlapping writers must not corrupt the shared
        // `.tmp` write-then-rename sequence for the same Creation id.
        var directory = Path.Combine(Path.GetTempPath(), $"node-runner-{Guid.NewGuid():N}");
        try
        {
            var repository = new FileCreationRepository(new TestStorageLocation(directory));
            var creation = CreateCreation("Concurrent");

            Parallel.For(0, 16, i =>
            {
                var withGeneration = new CreationDef(
                    creation.Id,
                    creation.Name,
                    creation.Creature,
                    TestTraining.State(i + 1, 1, TestTraining.Run));
                repository.Save(withGeneration);
            });

            var result = repository.Get(creation.Id);
            result.ShouldNotBeNull();
            result.Training!.Generation.ShouldBeInRange(1, 16);
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
    public void File_ListWithOneCorruptCreationFile_SkipsItButReturnsTheRest()
    {
        // Regression guard for #114: a single unreadable/corrupt file must
        // not empty the whole Creations list.
        var directory = Path.Combine(Path.GetTempPath(), $"node-runner-{Guid.NewGuid():N}");
        try
        {
            var repository = new FileCreationRepository(new TestStorageLocation(directory));
            var healthy = CreateCreation("Healthy");
            repository.Save(healthy);

            var corruptFolder = Path.Combine(directory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(corruptFolder);
            File.WriteAllText(Path.Combine(corruptFolder, FileCreationRepository.CreationFileName), "{ this is not valid json");

            var listed = repository.List();

            listed.Count.ShouldBe(1);
            listed[0].Id.ShouldBe(healthy.Id);
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
    public void File_GetWithCorruptCreationFile_ReturnsNullInsteadOfThrowing()
    {
        // Regression guard for #114: Get() must handle a corrupt file for
        // the currently active Creation the same way List() does, instead
        // of throwing out of a caller like LoadRouteCreation.
        var directory = Path.Combine(Path.GetTempPath(), $"node-runner-{Guid.NewGuid():N}");
        try
        {
            var id = Guid.NewGuid();
            var folder = Path.Combine(directory, id.ToString("N"));
            Directory.CreateDirectory(folder);
            var repository = new FileCreationRepository(new TestStorageLocation(directory));
            File.WriteAllText(Path.Combine(folder, FileCreationRepository.CreationFileName), "{ this is not valid json");

            repository.Get(id).ShouldBeNull();
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

    [Fact]
    public void InMemoryProgression_SaveAndLoad_RoundTripsSeeding()
    {
        var repository = new InMemoryProgressionRepository();
        var progression = new ProgressionDef(DefaultCreationsSeeded: true);

        repository.Save(progression);

        repository.Load().ShouldBe(progression);
    }

    [Fact]
    public void FileProgression_SaveAndLoad_RoundTripsSeeding()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"node-runner-progression-{Guid.NewGuid():N}");
        try
        {
            var repository = new FileProgressionRepository(new TestStorageLocation(directory));
            var progression = new ProgressionDef(DefaultCreationsSeeded: true);

            repository.Save(progression);

            repository.Load().ShouldBe(progression);
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
    public void FileProgression_LoadWithUnknownField_TreatsItAsCorrupt()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"node-runner-progression-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "progression.json");
            File.WriteAllText(path, """{ "removedFlag": true, "defaultCreationsSeeded": true }""");
            var repository = new FileProgressionRepository(new TestStorageLocation(directory));

            repository.Load().ShouldBe(new ProgressionDef());

            File.Exists(path).ShouldBeFalse();
            Directory.EnumerateFiles(directory, "progression.json.corrupt-*").ShouldNotBeEmpty();
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
    public void FileProgression_LoadWithTruncatedJson_ResetsToDefaultsAndQuarantinesFile()
    {
        // Regression guard for #114: a genuinely malformed file must not
        // crash the reader that runs on nearly every generation completion.
        var directory = Path.Combine(Path.GetTempPath(), $"node-runner-progression-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "progression.json");
            File.WriteAllText(path, "{ not valid json");
            var repository = new FileProgressionRepository(new TestStorageLocation(directory));

            var progression = repository.Load();

            progression.ShouldBe(new ProgressionDef());
            File.Exists(path).ShouldBeFalse();
            Directory.EnumerateFiles(directory, "progression.json.corrupt-*").ShouldNotBeEmpty();
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
    public void FileProgression_ConcurrentLoadAndSaveAcrossInstances_NeverQuarantinesAValidFile()
    {
        // Regression guard for #114: the per-path lock must be shared
        // across FileProgressionRepository instances (not just within a
        // single instance), otherwise one instance's Load() could race a
        // different instance's Save() and quarantine away a file that was
        // actually just written successfully.
        var directory = Path.Combine(Path.GetTempPath(), $"node-runner-progression-{Guid.NewGuid():N}");
        try
        {
            var storageLocation = new TestStorageLocation(directory);
            new FileProgressionRepository(storageLocation).Save(new ProgressionDef(DefaultCreationsSeeded: true));

            Parallel.For(0, 16, i =>
            {
                // A fresh instance per iteration exercises the cross-instance
                // path, not just cross-call reuse of the same object.
                var repository = new FileProgressionRepository(storageLocation);
                repository.Save(new ProgressionDef(DefaultCreationsSeeded: true));
                repository.Load();
            });

            Directory.EnumerateFiles(directory, "progression.json.corrupt-*").ShouldBeEmpty();
            new FileProgressionRepository(storageLocation).Load().DefaultCreationsSeeded.ShouldBeTrue();
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
    public void FileProgression_LoadWithMissingFields_TreatsItAsCorruptRatherThanFresh()
    {
        // Regression guard for #114: ProgressionDef's constructor defaults
        // make "{}" a *valid* fresh progression, so structurally incomplete
        // JSON must be distinguished from a genuinely fresh install instead
        // of silently resetting the unlock state.
        var directory = Path.Combine(Path.GetTempPath(), $"node-runner-progression-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "progression.json");
            File.WriteAllText(path, "{}");
            var repository = new FileProgressionRepository(new TestStorageLocation(directory));

            var progression = repository.Load();

            progression.ShouldBe(new ProgressionDef());
            File.Exists(path).ShouldBeFalse();
            Directory.EnumerateFiles(directory, "progression.json.corrupt-*").ShouldNotBeEmpty();
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
    public void FileProgression_LoadWithNonObjectJson_TreatsItAsCorruptRatherThanCrashing()
    {
        // Regression guard for #114: valid-but-non-object JSON (e.g. a bare
        // array) must not throw when checking for required fields.
        var directory = Path.Combine(Path.GetTempPath(), $"node-runner-progression-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "progression.json");
            File.WriteAllText(path, "[]");
            var repository = new FileProgressionRepository(new TestStorageLocation(directory));

            var progression = repository.Load();

            progression.ShouldBe(new ProgressionDef());
            File.Exists(path).ShouldBeFalse();
            Directory.EnumerateFiles(directory, "progression.json.corrupt-*").ShouldNotBeEmpty();
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
    public void FileCreation_SaveAndReload_KeepsNamesAndRetiredIds()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"node-runner-ids-{Guid.NewGuid():N}");
        try
        {
            var builder = new CreatureBuilder();
            var left = builder.AddNode(new Vector2D(0, 0), 1);
            var right = builder.AddNode(new Vector2D(2, 0), 1);
            var beam = builder.AddBeam(left, right);
            var extra = builder.AddNode(new Vector2D(4, 0), 1);
            builder.Rename(left, "Hip");
            builder.Rename(beam, "Thigh");
            builder.RemoveNode(extra);
            var creation = new CreationDef(Guid.NewGuid(), "Named", builder.Build(), null);
            var repository = new FileCreationRepository(new TestStorageLocation(directory));

            repository.Save(creation);
            var reloaded = repository.Get(creation.Id)!.Creature;

            reloaded.Nodes[reloaded.NodeIndexOf(left)].Name.ShouldBe("Hip");
            reloaded.Beams[reloaded.BeamIndexOf(beam)].Name.ShouldBe("Thigh");
            reloaded.NextPartId.ShouldBe(extra + 1);
            new CreatureBuilder(reloaded).AddNode(new Vector2D(6, 0), 1).ShouldBeGreaterThan(extra);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static CreationDef CreateCreation(string name)
    {
        return new CreationDef(
            Guid.NewGuid(),
            name,
            new CreatureDef(
                [new NodeDef(1, new Vector2D(0, 0), 1), new NodeDef(2, new Vector2D(2, 0), 1), new NodeDef(3, new Vector2D(4, 0), 1)],
                [new BeamDef(101, 1, 2), new BeamDef(102, 2, 3)],
                [new SensorDef(201, 101, SensorKind.Accelerometer), new SensorDef(202, 102, SensorKind.Camera)]),
            new TrainingStateDef(
                TestTraining.Brain,
                5,
                new TrainingRunDef(40.25, 88.25, 12, MapIds.Flat),
                new TrainingBestDef(2, 42.5, MapIds.Flat)));
    }

    private static void AssertEquivalent(CreationDef? actual, CreationDef expected)
    {
        actual.ShouldNotBeNull();
        actual.Id.ShouldBe(expected.Id);
        actual.Name.ShouldBe(expected.Name);
        actual.Creature.Nodes.ToArray().ShouldBe(expected.Creature.Nodes.ToArray());
        actual.Creature.Beams.ToArray().ShouldBe(expected.Creature.Beams.ToArray());
        actual.Creature.Sensors.ToArray().ShouldBe(expected.Creature.Sensors.ToArray());
        actual.Training.ShouldNotBeNull();
        actual.Training.Brain.Neurons.ToArray().ShouldBe(expected.Training!.Brain.Neurons.ToArray());
        actual.Training.Brain.Connections.ToArray().ShouldBe(expected.Training.Brain.Connections.ToArray());
        actual.Training.Brain.NextNeuronId.ShouldBe(expected.Training.Brain.NextNeuronId);
        actual.Training.Generation.ShouldBe(expected.Training.Generation);
        actual.Training.Latest.ShouldBe(expected.Training.Latest);
        actual.Training.Best.ShouldBe(expected.Training.Best);
    }

    private sealed class TestStorageLocation(string directoryPath) : IStorageLocation
    {
        public string DirectoryPath { get; } = directoryPath;
    }
}
