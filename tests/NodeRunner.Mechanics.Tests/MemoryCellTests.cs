namespace NodeRunner.Mechanics.Tests;

public sealed class MemoryCellTests
{
    // m moves the write's share of the way to the target: 0 keeps it, 1 takes the target.
    [Theory]
    [InlineData(0.5, -1, 0, 0.5)]
    [InlineData(0.5, -1, 1, -1)]
    [InlineData(0.5, -1, 0.25, 0.125)]
    [InlineData(-0.2, 0.8, 0.5, 0.3)]
    public void Next_MovesTheWritesShareOfTheWayToTheTarget(double m, double target, double write, double expected)
    {
        MemoryCell.Next(m, target, write).ShouldBe(expected, 1e-12);
    }
}
