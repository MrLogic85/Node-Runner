namespace NodeRunner.Mechanics.Tests;

public sealed class TearTests
{
    // The #1255 limit: 20 world units, 0.2 m, for 4 ticks in a row.
    [Fact]
    public void Limit_Is20UnitsFor4Ticks()
    {
        Tear.DriftLimit.ShouldBe(20);
        Tear.Ticks.ShouldBe(4);
    }

    [Theory]
    [InlineData(0, 19.99, 0)]
    [InlineData(3, 19.99, 0)]
    [InlineData(0, 20, 1)]
    [InlineData(2, 35, 3)]
    [InlineData(4, 35, 4)]
    public void NextTicksOver_CountsTicksAtOrOverTheLimitInARow(int before, double drift, int after)
    {
        Tear.NextTicksOver(before, drift).ShouldBe(after);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void NextTicksOver_CountsADriftThatIsNoNumberAsOver(double drift)
    {
        Tear.NextTicksOver(1, drift).ShouldBe(2);
    }

    [Fact]
    public void AnEnd_LetsGoOnTheFourthTickInARowOverTheLimit()
    {
        var ticksOver = 0;
        var letGo = new List<bool>();
        foreach (var drift in new[] { 25.0, 30, 21, 20 })
        {
            ticksOver = Tear.NextTicksOver(ticksOver, drift);
            letGo.Add(Tear.LetsGo(ticksOver));
        }

        letGo.ShouldBe([false, false, false, true]);
    }

    // One tick under the limit (a bounce settling) starts the count over.
    [Fact]
    public void AnEnd_DoesNotLetGoWhenATickDipsUnderTheLimit()
    {
        var ticksOver = 0;
        foreach (var drift in new[] { 25.0, 30, 21, 19, 25, 25, 25 })
        {
            ticksOver = Tear.NextTicksOver(ticksOver, drift);
        }

        Tear.LetsGo(ticksOver).ShouldBeFalse();
    }

    [Fact]
    public void MainPiece_IsThePieceWithTheMostJoints()
    {
        Tear.MainPiece([[1, 2], [3, 4, 5], [6]]).ShouldBe([3, 4, 5]);
    }

    // Pieces are listed by their first joint in joint order, so the first listed has the lowest joint index.
    [Fact]
    public void MainPiece_OnATie_IsTheFirstListed()
    {
        Tear.MainPiece([[7, 1], [3, 9]]).ShouldBe([7, 1]);
    }

    [Fact]
    public void MainPiece_IsEmptyWhenNoLinkJoinsAnything()
    {
        Tear.MainPiece([]).ShouldBeEmpty();
    }
}
