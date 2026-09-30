using NodeRunner.App.Lifecycle;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Lifecycle;

public sealed class CreationLockTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(40, true)]
    public void IsLocked_OnceAtLeastOneGenerationIsTrained(int? generation, bool locked)
    {
        var training = generation is { } trained ? new TrainingStateDef([2, 1], [0.1, 0.2, 0.3], trained, "Tanh") : null;
        var creature = new CreatureDef(
            [new NodeDef(new Vector2D(0, 0), 1), new NodeDef(new Vector2D(2, 0), 1)],
            [new BeamDef(0, 1)],
            [new CoreDef(0)]);

        CreationLock.IsLocked(new CreationDef(Guid.NewGuid(), "Worm", creature, training)).ShouldBe(locked);
    }
}
