namespace NodeRunner.Mechanics.Tests;

public sealed class MemoryCellTests
{
    private const double _tick = 1.0 / 60;

    // m moves write · delta / WriteTime of the way to the target: write 0 keeps it, and a full write for
    // WriteTime lands on the target without passing it.
    [Theory]
    [InlineData(0.5, -1, 0, _tick, 0.5)]
    [InlineData(0.5, -1, 1, _tick, 0.5 - (1.5 / 60))]
    [InlineData(0.5, -1, 0.5, _tick, 0.5 - (0.75 / 60))]
    [InlineData(0.5, -1, 1, MemoryCell.WriteTime, -1)]
    public void Next_MovesTheWritesRateOfTheWayToTheTarget(double m, double target, double write, double delta, double expected)
    {
        MemoryCell.Next(m, target, write, delta).ShouldBe(expected, 1e-12);
    }

    // WriteTime is a time constant: at full write m closes 63% of the gap in it and 95% in three times it.
    [Fact]
    public void Next_AtFullWrite_ClosesTheGapOverItsTimeConstant()
    {
        var m = MemoryCell.Start;

        var afterOne = Run(ref m, MemoryCell.WriteTime);
        afterOne.ShouldBe(0.63, tolerance: 0.01);

        var afterThree = Run(ref m, 2 * MemoryCell.WriteTime);
        afterThree.ShouldBe(0.95, tolerance: 0.01);
    }

    private static double Run(ref double m, double seconds)
    {
        for (var tick = 0; tick < Math.Round(seconds / _tick); tick++)
        {
            m = MemoryCell.Next(m, target: 1, write: 1, _tick);
        }

        return m;
    }
}
