using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class TearTests
{
    // An end lets go on the fourth tick in a row at or over the drift limit of 20 (a drift that is no number counts
    // as over); one tick under it, a bounce settling, starts the count over.
    [Fact]
    public void AnEnd_LetsGoOnTheFourthTickInARowOverTheLimit()
    {
        foreach (var (before, drift, after) in new[] { (0, 19.99, 0), (3, 19.99, 0), (0, 20, 1), (2, 35, 3), (4, 35, 4), (1, double.NaN, 2), (1, double.PositiveInfinity, 2) })
        {
            Tear.NextTicksOver(before, drift).ShouldBe(after, $"{before} then {drift}");
        }

        foreach (var (drifts, letsGo) in new[] { (new[] { 25.0, 30, 21 }, false), ([25.0, 30, 21, 20], true), ([25.0, 30, 21, 19, 25, 25, 25], false) })
        {
            Tear.LetsGo(drifts.Aggregate(0, Tear.NextTicksOver)).ShouldBe(letsGo, string.Join(", ", drifts));
        }
    }

    // Joints 1–2–3 in a row by beams 10 and 11, and 4–5 by beam 12. The main piece has the most joints, a tie going to
    // the first listed; a beam torn at either end no longer joins its joints, but a Piston never tears.
    [Fact]
    public void TheMainPiece_IsTheOneWithTheMostJoints()
    {
        foreach (var (pistons, torn, main) in new (PistonDef[], bool[], int[])[]
        {
            ([], new bool[6], [1, 2, 3]),
            ([], [false, false, false, true, false, false], [1, 2]),
            ([], [false, false, true, true, false, false], [1, 2]),
            ([new PistonDef(13, 3, 4)], [false, false, true, true, false, false], [3, 4, 5]),
            ([], [true, false, false, true, true, true], [1]),
        })
        {
            Tear.MainPiece(Chain(pistons), torn).ShouldBe(main, string.Join(", ", torn));
        }

        Should.Throw<ArgumentException>(() => Tear.MainPiece(Chain([]), new bool[5]));
    }

    private static CreatureDef Chain(PistonDef[] pistons) => new(
        Enumerable.Range(1, 5).Select(id => new NodeDef(id, new Vector2D(id * 100, 0))).ToArray(),
        [new BeamDef(10, 1, 2), new BeamDef(11, 2, 3), new BeamDef(12, 4, 5)],
        [],
        pistons,
        [],
        100);
}
