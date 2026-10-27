using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

public sealed class ExampleCopyWorkflowTests
{
    [Fact]
    public void Copy_SavesTheExampleUnderANewId_InThePlayersLanguage_OnTheGridDefault_AndRefusesAnUnknownExample()
    {
        var repository = new InMemoryCreationRepository();
        repository.Save(new CreationDef(Guid.NewGuid(), "Worm", Example().Creature));
        var example = Example();
        var copyId = Guid.NewGuid();
        UiText? asked = null;
        var defaultGrid = new BuildGridDef(25, snap: true);
        var workflow = new ExampleCopyWorkflow(repository, () => defaultGrid, [example], () => copyId);

        var copy = workflow.Copy(example.Id, text =>
        {
            asked = text;
            return "Gångare";
        });

        asked.ShouldBe(example.Name);
        copy.ShouldBe(new CreationDef(copyId, "Gångare", example.Creature, example.Training) { Grid = defaultGrid });
        repository.Get(copyId).ShouldBe(copy);
        Should.Throw<KeyNotFoundException>(() => workflow.Copy(Guid.NewGuid(), TestLanguage.Untranslated));
    }

    // A name already taken, in any case, becomes "Copy of" the name, cut to the creation limit.
    [Theory]
    [InlineData("Kopia av Walker", "Kopia av Walker")]
    [InlineData("KKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKK", null)]
    public void Copy_ANameTaken_IsACopyOfTheName(string translated, string? expected)
    {
        var repository = new InMemoryCreationRepository();
        var example = Example();
        repository.Save(new CreationDef(Guid.NewGuid(), "walker", example.Creature));

        var copy = new ExampleCopyWorkflow(repository, () => BuildGridDef.Default, [example]).Copy(example.Id, text =>
            text.Equals(UiText.Format("Copy of {0}", "Walker")) ? translated : text.Message);

        copy.Name.ShouldBe(expected ?? translated[..NameLimits.Creation]);
    }

    private static CreationExample Example() =>
        new(
            Guid.NewGuid(),
            UiText.Plain("Walker"),
            UiText.Plain("Servos in the knees: the basic walk."),
            new CreatureDef(
                [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0))],
                [new BeamDef(101, 1, 2)],
                [new SensorDef(201, 101, SensorKind.Accelerometer)]),
            TestTraining.State(8, 1, TestTraining.Run));
}
