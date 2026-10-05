using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests;

public sealed class ShadowStripPresentationTests
{
    [Fact]
    public void UpToEightShadows_EachGetsACell_ShadowOneOnTheRight()
    {
        var strip = new ShadowStripPresentation();

        var view = strip.View(Standings(1, 2, 3), generation: 0);

        view.Cells.Select(cell => cell.Number).ShouldBe([3, 2, 1]);
        view.Pages.ShouldBeFalse();
        view.CanPageWorse.ShouldBeFalse();
        view.Trailing.ShouldBe(ShadowStripTrailing.None);
    }

    [Fact]
    public void Bars_MeasureAgainstThisGenerationsLeader_WhoseBarIsFull()
    {
        var strip = new ShadowStripPresentation();

        strip.View(Standings(5, 2), 0).Cells.Select(cell => cell.Fill).ShouldBe([0.4, 1]);
        strip.View(Standings(20, 5), 0).Cells.Select(cell => cell.Fill).ShouldBe([0.25, 1]);
    }

    [Fact]
    public void Bars_AreEmptyForShadowsBehindTheStartOrNotRunning()
    {
        var strip = new ShadowStripPresentation();

        var view = strip.View(Standings(-1, double.NaN, 0), 0);

        view.Cells.ShouldAllBe(cell => cell.Fill == 0);
    }

    [Fact]
    public void Cells_MarkOnlyTheFollowedShadow()
    {
        var strip = new ShadowStripPresentation();
        ShadowStanding[] shadows =
        [
            new(1, 1, IsFollowed: true, IsLeader: false, IsPreviousBest: true),
            new(2, 3, IsFollowed: false, IsLeader: true, IsPreviousBest: false),
        ];

        strip.View(shadows, 0).Cells.ShouldBe(
        [
            new ShadowStripCell(2, 1, IsFollowed: false),
            new ShadowStripCell(1, 1.0 / 3, IsFollowed: true),
        ]);
    }

    [Fact]
    public void PastEightShadows_TheFirstPageShowsTheTopSixAndASortButton()
    {
        var strip = new ShadowStripPresentation();

        var view = strip.View(Standings(Enumerable.Repeat(1.0, 16)), 0);

        view.Cells.Select(cell => cell.Number).ShouldBe([6, 5, 4, 3, 2, 1]);
        view.Pages.ShouldBeTrue();
        view.CanPageWorse.ShouldBeTrue();
        view.Trailing.ShouldBe(ShadowStripTrailing.Sort);
    }

    [Fact]
    public void PagingWorse_ShowsWorseShadows_AndTheLastPageEndsWithTheWorstSix()
    {
        var strip = new ShadowStripPresentation();
        var shadows = Standings(Enumerable.Repeat(1.0, 16));
        strip.View(shadows, 0);

        strip.PageWorse();
        var second = strip.View(shadows, 0);
        strip.PageWorse();
        var last = strip.View(shadows, 0);
        strip.PageWorse();

        second.Cells.Select(cell => cell.Number).ShouldBe([12, 11, 10, 9, 8, 7]);
        second.Trailing.ShouldBe(ShadowStripTrailing.Better);
        last.Cells.Select(cell => cell.Number).ShouldBe([16, 15, 14, 13, 12, 11]);
        last.CanPageWorse.ShouldBeFalse();
        strip.View(shadows, 0).ShouldBeEquivalentTo(last);
    }

    [Fact]
    public void PagingBetter_StopsAtTheFirstPage()
    {
        var strip = new ShadowStripPresentation();
        var shadows = Standings(Enumerable.Repeat(1.0, 9));
        strip.View(shadows, 0);
        strip.PageWorse();

        strip.View(shadows, 0).Cells.Select(cell => cell.Number).ShouldBe([9, 8, 7, 6, 5, 4]);
        strip.PageBetter();
        strip.PageBetter();

        var view = strip.View(shadows, 0);
        view.Cells.Select(cell => cell.Number).ShouldBe([6, 5, 4, 3, 2, 1]);
        view.Trailing.ShouldBe(ShadowStripTrailing.Sort);
    }

    [Fact]
    public void Sorting_RanksByDistanceSoFar_WorstToBestLeftToRight_AndHoldsUntilSortedAgain()
    {
        var strip = new ShadowStripPresentation();
        double[] distances = [1, 9, double.NaN, 4, 8, 2, 7, 3, 6, 5];
        var shadows = Standings(distances);
        strip.View(shadows, 3);
        strip.PageWorse();

        strip.Sort(shadows, 3);
        var sorted = strip.View(shadows, 3);
        var later = strip.View(Standings(distances.Select(distance => -distance)), 3);

        sorted.Cells.Select(cell => cell.Number).ShouldBe([4, 10, 9, 7, 5, 2]);
        sorted.Trailing.ShouldBe(ShadowStripTrailing.Sort);
        later.Cells.Select(cell => cell.Number).ShouldBe([4, 10, 9, 7, 5, 2]);
    }

    [Fact]
    public void Sorting_PutsShadowsThatAreNotRunningLast()
    {
        var strip = new ShadowStripPresentation();
        var shadows = Standings(double.NaN, -2, 1, 0, 0, 0, 0, 0, 0);

        strip.Sort(shadows, 0);
        strip.PageWorse();

        strip.View(shadows, 0).Cells.Select(cell => cell.Number).First().ShouldBe(1);
    }

    [Fact]
    public void ANewGeneration_StartsOverFromShadowOrderOnTheFirstPage()
    {
        var strip = new ShadowStripPresentation();
        var shadows = Standings(1, 9, 4, 8, 2, 7, 3, 6, 5);
        strip.Sort(shadows, 0);
        strip.PageWorse();

        var view = strip.View(shadows, 1);

        view.Cells.Select(cell => cell.Number).ShouldBe([6, 5, 4, 3, 2, 1]);
        view.Trailing.ShouldBe(ShadowStripTrailing.Sort);
    }

    [Fact]
    public void MorePlaces_GiveEveryShadowACell_UntilTheyRunOut()
    {
        var strip = new ShadowStripPresentation { Places = 12 };

        var view = strip.View(Standings(Enumerable.Repeat(1.0, 12)), 0);

        view.Cells.Count.ShouldBe(12);
        view.Pages.ShouldBeFalse();
    }

    [Fact]
    public void MorePlaces_ShowMoreShadowsPerPage()
    {
        var strip = new ShadowStripPresentation { Places = 12 };

        var view = strip.View(Standings(Enumerable.Repeat(1.0, 30)), 0);

        view.Cells.Select(cell => cell.Number).ShouldBe([10, 9, 8, 7, 6, 5, 4, 3, 2, 1]);
        view.Trailing.ShouldBe(ShadowStripTrailing.Sort);
    }

    [Fact]
    public void Places_AreNeverFewerThanTheButtonsAndOneShadow()
    {
        var strip = new ShadowStripPresentation { Places = 1 };

        var view = strip.View(Standings(1, 2, 3, 4), 0);

        strip.Places.ShouldBe(ShadowStripPresentation.FewestPlaces);
        view.Cells.Select(cell => cell.Number).ShouldBe([1]);
        view.Pages.ShouldBeTrue();
    }

    [Fact]
    public void ChangingThePlaces_KeepsTheOrder_AndGoesToTheLastPageIfThePageIsGone()
    {
        var strip = new ShadowStripPresentation();
        var shadows = Standings(1, 9, 4, 8, 2, 7, 3, 6, 5, 0, 0, 0, 0, 0, 0, 0);
        strip.Sort(shadows, 0);
        strip.PageWorse();
        strip.PageWorse();

        strip.Places = 12;
        var view = strip.View(shadows, 0);

        view.Cells.Select(cell => cell.Number).ShouldBe([16, 15, 14, 13, 12, 11, 10, 1, 5, 7]);
        view.CanPageWorse.ShouldBeFalse();
        view.Trailing.ShouldBe(ShadowStripTrailing.Better);
        strip.PageBetter();
        strip.View(shadows, 0).Trailing.ShouldBe(ShadowStripTrailing.Sort);
    }

    [Fact]
    public void ChangingThePlaces_KeepsAPageThatStillExists()
    {
        var strip = new ShadowStripPresentation();
        var shadows = Standings(Enumerable.Repeat(1.0, 30));
        strip.View(shadows, 0);
        strip.PageWorse();

        strip.Places = 12;
        var view = strip.View(shadows, 0);

        view.Cells.Select(cell => cell.Number).ShouldBe([20, 19, 18, 17, 16, 15, 14, 13, 12, 11]);
        view.Trailing.ShouldBe(ShadowStripTrailing.Better);
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

    [Fact]
    public void NoShadows_ShowAnEmptyStrip()
    {
        new ShadowStripPresentation().View([], 0).ShouldBe(ShadowStripView.Empty);
    }

    private static ShadowStanding[] Standings(params double[] distances) => Standings((IEnumerable<double>)distances);

    private static ShadowStanding[] Standings(IEnumerable<double> distances) =>
        distances.Select((distance, index) => new ShadowStanding(index + 1, distance, false, false, false)).ToArray();
}
