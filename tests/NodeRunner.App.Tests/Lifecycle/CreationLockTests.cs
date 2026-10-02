using NodeRunner.App.Lifecycle;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Lifecycle;

public sealed class CreationLockTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData(1, true)]
    [InlineData(40, true)]
    public void IsLocked_OnceAtLeastOneGenerationIsTrained(int? generation, bool locked)
    {
        var training = generation is { } trained ? TestTraining.State(trained, 1, TestTraining.Run) : null;
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 1), new NodeDef(2, new Vector2D(2, 0), 1)],
            [new BeamDef(101, 1, 2)],
            [new SensorDef(201, 101, SensorKind.Accelerometer)]);

        CreationLock.IsLocked(new CreationDef(Guid.NewGuid(), "Worm", creature, training)).ShouldBe(locked);
    }
}
