using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

public sealed class ConstructionDraftWorkflowTests
{
    [Fact]
    public void BeginRebuildDraft_ClearsActiveCreationAndKeepsSourceCreature()
    {
        var original = CreateCreation("Original", generation: 4);
        var workflow = new ConstructionDraftWorkflow();

        var draft = workflow.BeginRebuildDraft(original.Creature);

        draft.ActiveCreationId.ShouldBeNull();
        draft.Creature.ShouldBe(original.Creature);
        draft.StatusMessage.ShouldBe("Rebuild creates a new Creation; previous training will not be copied.");
        original.Training!.Generation.ShouldBe(4);
    }

    [Fact]
    public void CompleteDraft_CreatesNewIdentityWithoutCopiedTraining()
    {
        var newId = Guid.NewGuid();
        var original = CreateCreation("Original", generation: 8);
        var workflow = new ConstructionDraftWorkflow(() => newId);

        var rebuilt = workflow.CompleteDraft(original.Creature, "Creation 2");

        rebuilt.Id.ShouldBe(newId);
        rebuilt.Id.ShouldNotBe(original.Id);
        rebuilt.Name.ShouldBe("Creation 2");
        rebuilt.Creature.ShouldBe(original.Creature);
        rebuilt.BrainShape.ShouldBe(BrainShapeDef.Default);
        rebuilt.Training.ShouldBeNull();
        original.Training!.Generation.ShouldBe(8);
    }

    [Fact]
    public void CompleteDraft_WithBrainShape_StoresShape()
    {
        var shape = new BrainShapeDef(3, 16);
        var creature = CreateCreation("Original", generation: 1).Creature;
        var workflow = new ConstructionDraftWorkflow(() => Guid.NewGuid());

        var draft = workflow.CompleteDraft(creature, "Creation 2", shape);

        draft.BrainShape.ShouldBe(shape);
    }

    [Fact]
    public void CompletingRebuiltDraft_DoesNotMutatePersistedOriginal()
    {
        var repository = new InMemoryCreationRepository();
        var original = CreateCreation("Original", generation: 12);
        repository.Save(original);
        var rebuiltId = Guid.NewGuid();
        var workflow = new ConstructionDraftWorkflow(() => rebuiltId);

        var draft = workflow.BeginRebuildDraft(original.Creature);
        var rebuilt = workflow.CompleteDraft(draft.Creature, "Creation 2");
        repository.Save(rebuilt);

        repository.Get(original.Id).ShouldBe(original);
        repository.Get(original.Id)!.Training!.BestGenome.ShouldBe([0.1, -0.2, 0.3]);
        repository.Get(original.Id)!.Training!.Generation.ShouldBe(12);
        repository.Get(rebuiltId)!.Training.ShouldBeNull();
        repository.List().Select(creation => creation.Id).ShouldBe([rebuiltId, original.Id], ignoreOrder: true);
    }

    [Fact]
    public void BeginRebuildDraft_WithNullCreature_Throws()
    {
        var workflow = new ConstructionDraftWorkflow();

        Should.Throw<ArgumentNullException>(() => workflow.BeginRebuildDraft(null!));
    }

    [Fact]
    public void CompleteDraft_WithNullCreature_Throws()
    {
        var workflow = new ConstructionDraftWorkflow();

        Should.Throw<ArgumentNullException>(() => workflow.CompleteDraft(null!, "Creation 2"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void CompleteDraft_WithBlankName_Throws(string name)
    {
        var workflow = new ConstructionDraftWorkflow();
        var creature = CreateCreation("Original", generation: 1).Creature;

        Should.Throw<ArgumentException>(() => workflow.CompleteDraft(creature, name));
    }

    private static CreationDef CreateCreation(string name, int generation)
    {
        return new CreationDef(
            Guid.NewGuid(),
            name,
            new CreatureDef(
                [new NodeDef(new Vector2D(0, 0), 1), new NodeDef(new Vector2D(2, 0), 1)],
                [new BeamDef(0, 1)],
                [new CoreDef(0)]),
            new TrainingStateDef([2, 1], [0.1, -0.2, 0.3], generation, "Tanh"));
    }
}
