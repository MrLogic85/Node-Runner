namespace NodeRunner.Domain.Tests;

public sealed class BuildGridDefTests
{
    // A hand-edited size moves to the nearest choice, a tie to the larger.
    [Theory]
    [InlineData(25, 25)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(37, 25)]
    [InlineData(38, 50)]
    [InlineData(75, 100)]
    [InlineData(0, 25)]
    [InlineData(int.MinValue, 25)]
    [InlineData(int.MaxValue, 100)]
    public void Size_SnapsToTheNearestChoice(int saved, int expected)
    {
        new BuildGridDef(saved, snap: false).SizeCm.ShouldBe(expected);
        (BuildGridDef.Default with { SizeCm = saved }).SizeCm.ShouldBe(expected);
    }
}
