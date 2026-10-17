using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests;

public sealed class ShadowStripPresentationTests
{
    [Fact]
    public void UpToEightShadows_EachGetsACell_ShadowOneOnTheRight_OnlyTheFollowedMarkedAndCappedOnATear()
    {
        var strip = new ShadowStripPresentation();
        ShadowStanding[] shadows =
        [
            new(1, _metre, IsFollowed: true, IsLeader: false, IsPreviousBest: true),
            new(2, 3 * _metre, IsFollowed: false, IsLeader: true, IsPreviousBest: false),
            new(3, 2 * _metre, IsFollowed: false, IsLeader: false, IsPreviousBest: false),
        ];

        var view = strip.View(shadows, 0);
        var torn = strip.View(shadows, 0, followedCrashed: true);

        view.Cells.ShouldBe([new ShadowStripCell(3, 2.0 / 3, IsFollowed: false), new ShadowStripCell(2, 1, IsFollowed: false), new ShadowStripCell(1, 1.0 / 3, IsFollowed: true)]);
        (view.Pages, view.CanPageWorse, view.Trailing).ShouldBe((false, false, ShadowStripTrailing.None));
        torn.Cells.Select(cell => cell.IsCapped).ShouldBe([false, false, true]);
        strip.View([], 0).ShouldBe(ShadowStripView.Empty);
    }

    // Against this generation's leader, whose bar is full, but at least a metre, so a leader a
    // few millimetres out fills only a sliver; empty behind the start or not running.
    [Theory]
    [InlineData(new[] { 5.0, 2.0 }, new[] { 0.4, 1.0 })]
    [InlineData(new[] { 0.01, 0.008 }, new[] { 0.008, 0.01 })]
    [InlineData(new[] { 0.5 }, new[] { 0.5 })]
    [InlineData(new[] { -0.01, double.NaN, 0 }, new[] { 0.0, 0.0, 0.0 })]
    public void Bars_MeasureAgainstTheLeaderOrAMetreAtLeast(double[] metres, double[] fills)
    {
        var strip = new ShadowStripPresentation();

        var view = strip.View(Standings(metres.Select(distance => distance * _metre)), 0);

        view.Cells.Select(cell => cell.Fill).ShouldBe(fills, tolerance: 1e-9);
    }

    [Fact]
    public void PastEightShadows_ItPagesSixAtATime_TheLastPageEndingWithTheWorstSix()
    {
        var strip = new ShadowStripPresentation();
        var shadows = Standings(Enumerable.Repeat(1.0, 16));
        var pages = new List<ShadowStripView> { strip.View(shadows, 0) };

        strip.PageWorse();
        pages.Add(strip.View(shadows, 0));
        strip.PageWorse();
        pages.Add(strip.View(shadows, 0));
        strip.PageWorse();
        pages.Add(strip.View(shadows, 0));
        strip.PageBetter();
        strip.PageBetter();
        strip.PageBetter();
        pages.Add(strip.View(shadows, 0));

        pages.Select(page => page.Cells.Select(cell => cell.Number)).ShouldBe(
        [
            [6, 5, 4, 3, 2, 1],
            [12, 11, 10, 9, 8, 7],
            [16, 15, 14, 13, 12, 11],
            [16, 15, 14, 13, 12, 11],
            [6, 5, 4, 3, 2, 1],
        ]);
        pages.Select(page => page.Trailing).ShouldBe([ShadowStripTrailing.Sort, ShadowStripTrailing.Better, ShadowStripTrailing.Better, ShadowStripTrailing.Better, ShadowStripTrailing.Sort]);
        pages.Select(page => page.CanPageWorse).ShouldBe([true, true, false, false, true]);
        pages.ShouldAllBe(page => page.Pages);
    }

    [Fact]
    public void Sorting_RanksByDistanceSoFar_NotRunningLast_AndHoldsUntilANewGeneration()
    {
        var strip = new ShadowStripPresentation();
        double[] distances = [1, 9, double.NaN, 4, 8, 2, 7, 3, 6, 5];
        var shadows = Standings(distances);
        strip.View(shadows, 3);
        strip.PageWorse();

        strip.Sort(shadows, 3);
        var sorted = strip.View(shadows, 3);
        var later = strip.View(Standings(distances.Select(distance => -distance)), 3);
        strip.PageWorse();
        var worst = strip.View(shadows, 3);
        var nextGeneration = strip.View(shadows, 4);

        sorted.Cells.Select(cell => cell.Number).ShouldBe([4, 10, 9, 7, 5, 2]);
        sorted.Trailing.ShouldBe(ShadowStripTrailing.Sort);
        later.Cells.Select(cell => cell.Number).ShouldBe([4, 10, 9, 7, 5, 2]);
        worst.Cells.Select(cell => cell.Number).ShouldBe([3, 1, 6, 8, 4, 10]);
        nextGeneration.Cells.Select(cell => cell.Number).ShouldBe([6, 5, 4, 3, 2, 1]);
        nextGeneration.Trailing.ShouldBe(ShadowStripTrailing.Sort);
    }

    // Two places go to the buttons once the strip pages; never fewer than those and one shadow.
    [Theory]
    [InlineData(12, 12, 12, false)]
    [InlineData(12, 30, 10, true)]
    [InlineData(1, 4, 1, true)]
    public void Places_SetHowManyShadowsShowAtOnce(int places, int shadows, int cells, bool pages)
    {
        var strip = new ShadowStripPresentation { Places = places };
        strip.Places.ShouldBe(Math.Max(places, ShadowStripPresentation.FewestPlaces));

        var view = strip.View(Standings(Enumerable.Repeat(1.0, shadows)), 0);

        view.Cells.Select(cell => cell.Number).ShouldBe(Enumerable.Range(1, cells).Reverse());
        view.Pages.ShouldBe(pages);
    }

    [Fact]
    public void ChangingThePlaces_KeepsTheOrder_AndThePage_OrGoesToTheLastPageIfThePageIsGone()
    {
        var strip = new ShadowStripPresentation();
        var shadows = Standings(1, 9, 4, 8, 2, 7, 3, 6, 5, 0, 0, 0, 0, 0, 0, 0);
        strip.Sort(shadows, 0);
        strip.PageWorse();
        strip.PageWorse();
        var kept = new ShadowStripPresentation();
        var thirty = Standings(Enumerable.Repeat(1.0, 30));
        kept.View(thirty, 0);
        kept.PageWorse();

        strip.Places = 12;
        kept.Places = 12;
        var view = strip.View(shadows, 0);
        var keptView = kept.View(thirty, 0);

        view.Cells.Select(cell => cell.Number).ShouldBe([16, 15, 14, 13, 12, 11, 10, 1, 5, 7]);
        view.CanPageWorse.ShouldBeFalse();
        view.Trailing.ShouldBe(ShadowStripTrailing.Better);
        keptView.Cells.Select(cell => cell.Number).ShouldBe([20, 19, 18, 17, 16, 15, 14, 13, 12, 11]);
        keptView.Trailing.ShouldBe(ShadowStripTrailing.Better);
    }

    [Fact]
    public void Shown_IsEveryShadow_UntilTheStripPages_ThenTheCurrentPageBestFirst()
    {
        var strip = new ShadowStripPresentation();

        strip.Shown(8, generation: 0).ShouldBe([0, 1, 2, 3, 4, 5, 6, 7]);
        strip.Shown(9, generation: 0).ShouldBe([0, 1, 2, 3, 4, 5]);
        strip.PageWorse();
        strip.Shown(9, generation: 0).ShouldBe([3, 4, 5, 6, 7, 8]);
        strip.Shown(9, generation: 1).ShouldBe([0, 1, 2, 3, 4, 5]);
        strip.Shown(0, generation: 1).ShouldBeEmpty();
    }

    private const double _metre = Metres.WorldUnitsPerMetre;

    private static ShadowStanding[] Standings(params double[] distances) => Standings((IEnumerable<double>)distances);

    private static ShadowStanding[] Standings(IEnumerable<double> distances) =>
        distances.Select((distance, index) => new ShadowStanding(index + 1, distance, false, false, false)).ToArray();
}
