namespace NodeRunner.Domain.Tests;

public sealed class RunLengthRangeDefTests
{
    // A hand-edited end is clamped to the slider and moved to the nearest 5 s step, a tie up; inverted ends swap.
    [Theory]
    [InlineData(10, 60, 10, 60)]
    [InlineData(30, 30, 30, 30)]
    [InlineData(60, 10, 10, 60)]
    [InlineData(7, 8, 5, 10)]
    [InlineData(6, 9, 5, 10)]
    [InlineData(0, 100, 5, 60)]
    [InlineData(int.MinValue, int.MaxValue, 5, 60)]
    public void Ends_AreClampedSnappedAndOrdered(int low, int high, int expectedLow, int expectedHigh)
    {
        var range = new RunLengthRangeDef(low, high);

        (range.LowSeconds, range.HighSeconds).ShouldBe((expectedLow, expectedHigh));
    }
}
