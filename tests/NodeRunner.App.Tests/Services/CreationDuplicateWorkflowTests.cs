using System.Globalization;
using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

public sealed class CreationDuplicateWorkflowTests
{
    [Fact]
    public void Duplicate_SavesATrainedCopyOnTheSameGrid_NamedACopyInThePlayersLanguage()
    {
        var repository = new InMemoryCreationRepository();
        var source = Creation("Walker");
        repository.Save(source);
        var copyId = Guid.NewGuid();
        UiText? asked = null;

        var copy = new CreationDuplicateWorkflow(repository, () => copyId).Duplicate(source.Id, text =>
        {
            asked = text;
            return "Kopia av Walker";
        });

        asked.ShouldBe(UiText.Format("Copy of {0}", "Walker"));
        copy.ShouldBe(new CreationDef(copyId, "Kopia av Walker", source.Creature, source.Training) { Grid = source.Grid });
        repository.Get(copyId).ShouldBe(copy);
        Should.Throw<KeyNotFoundException>(() => new CreationDuplicateWorkflow(repository).Duplicate(Guid.NewGuid(), TestLanguage.Untranslated));
    }

    [Fact]
    public void Duplicate_ALongName_CutsTheCopyNameToTheCreationLimit()
    {
        var repository = new InMemoryCreationRepository();
        var source = Creation(new string('W', NameLimits.Creation));
        repository.Save(source);

        var copy = new CreationDuplicateWorkflow(repository).Duplicate(source.Id, text => string.Format(CultureInfo.InvariantCulture, text.Message, [.. text.Args]));

        copy.Name.ShouldBe("Copy of " + new string('W', NameLimits.Creation - "Copy of ".Length));
        repository.Get(source.Id)!.Name.ShouldBe(source.Name);
    }

    private static CreationDef Creation(string name) =>
        new(
            Guid.NewGuid(),
            name,
            new CreatureDef(
                [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0))],
                [new BeamDef(101, 1, 2)],
                [new SensorDef(201, 101, SensorKind.Accelerometer)]),
            TestTraining.State(8, 1, TestTraining.Run))
        {
            Grid = new BuildGridDef(25, snap: true),
        };
}
