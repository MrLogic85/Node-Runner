using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class TearTests
{
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

    // Joints 1–2–3 in a row by beams 10 and 11, and 4–5 by beam 12.
    private static CreatureDef Chain(PistonDef[]? pistons = null) => new(
        Enumerable.Range(1, 5).Select(id => new NodeDef(id, new Vector2D(id * 100, 0))).ToArray(),
        [new BeamDef(10, 1, 2), new BeamDef(11, 2, 3), new BeamDef(12, 4, 5)],
        [],
        pistons ?? [],
        [],
        100);

    [Fact]
    public void MainPiece_IsThePieceWithTheMostJoints()
    {
        Tear.MainPiece(Chain(), new bool[6]).ShouldBe([1, 2, 3]);
    }

    // A beam torn at one end no longer joins its joints, so 1–2–3 splits into 1–2 and 3 alone.
    [Fact]
    public void MainPiece_LeavesOutABeamTornAtOneEnd()
    {
        Tear.MainPiece(Chain(), [false, false, false, true, false, false]).ShouldBe([1, 2]);
    }

    // With beam 11 gone at both ends the creature is 1–2 and 4–5: a tie, which the first listed wins.
    [Fact]
    public void MainPiece_OnATie_IsThePieceWithTheFirstJoint()
    {
        Tear.MainPiece(Chain(), [false, false, true, true, false, false]).ShouldBe([1, 2]);
    }

    // A Piston never tears, so it still joins 3 to 4 when beam 11 has let go.
    [Fact]
    public void MainPiece_CountsPistonsAndSpringsAsHolding()
    {
        var creature = Chain([new PistonDef(13, 3, 4)]);

        Tear.MainPiece(creature, [false, false, true, true, false, false]).ShouldBe([3, 4, 5]);
    }

    [Fact]
    public void MainPiece_WithEveryLinkTorn_IsTheFirstJoint()
    {
        Tear.MainPiece(Chain(), [true, false, false, true, true, true]).ShouldBe([1]);
    }

    [Fact]
    public void MainPiece_NeedsBothEndsOfEveryBeam()
    {
        Should.Throw<ArgumentException>(() => Tear.MainPiece(Chain(), new bool[5]));
    }
}
