using NodeRunner.Domain;

namespace NodeRunner.App.Tests;

internal static class TestTraining
{
    public static TrainingRunDef Run { get; } = new(1, 1, 0, MapIds.Flat);
}
