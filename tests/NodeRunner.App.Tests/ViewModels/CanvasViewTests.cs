using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public class CanvasViewTests
{
    // 4000×2000 bounds seen through a window that holds them at 0.25×.
    private static readonly CanvasRect _area = new(new Vector2D(-2000, -1000), new Vector2D(2000, 1000));
    private static readonly CanvasRect _screen = new(new Vector2D(0, 0), new Vector2D(1000, 500));
    private static readonly Vector2D _middle = _screen.Center;

    [Fact]
    public void NewView_MapsViewAndCanvasOneToOne()
    {
        var view = new CanvasView(_area);

        view.ToCanvas(new Vector2D(40, -20)).ShouldBe(new Vector2D(40, -20));
        view.ToView(new Vector2D(40, -20)).ShouldBe(new Vector2D(40, -20));
    }

    [Fact]
    public void ZoomAbout_KeepsTheCanvasPointUnderTheFocusInPlace()
    {
        var view = Centred();
        var focus = new Vector2D(200, 120);
        var under = view.ToCanvas(focus);

        view.ZoomAbout(focus, 2);

        view.Zoom.ShouldBe(2);
        view.ToCanvas(focus).X.ShouldBe(under.X, 1e-9);
        view.ToCanvas(focus).Y.ShouldBe(under.Y, 1e-9);
    }

    [Fact]
    public void ZoomAbout_StopsAtMaxZoomAndAtShowingTheWholeArea()
    {
        var view = Centred();

        view.ZoomAbout(_middle, 100);
        view.Zoom.ShouldBe(CanvasView.MaxZoom);

        view.ZoomAbout(_middle, 0.001);
        view.Zoom.ShouldBe(0.25);
        view.MinZoom.ShouldBe(0.25);
        view.ToView(_area.Min).ShouldBe(new Vector2D(0, 0));
        view.ToView(_area.Max).ShouldBe(new Vector2D(1000, 500));
    }

    [Fact]
    public void MinZoom_ShowsTheWholeAreaWhenItsShapeDiffersFromTheScreen()
    {
        var view = new CanvasView(new CanvasRect(new Vector2D(-500, -500), new Vector2D(500, 500))) { VisibleArea = _screen };

        view.ZoomAbout(_middle, 0.001);

        view.Zoom.ShouldBe(0.5);
        view.ToView(new Vector2D(-500, -500)).ShouldBe(new Vector2D(250, 0));
        view.ToView(new Vector2D(500, 500)).ShouldBe(new Vector2D(750, 500));
    }

    [Fact]
    public void ZoomAbout_AtALimit_LeavesTheViewAndRaisesNothing()
    {
        var view = Centred();
        view.ZoomAbout(_middle, 100);
        var offset = view.Offset;
        var changes = 0;
        view.Changed += (_, _) => changes++;

        view.ZoomAbout(new Vector2D(50, 50), 2);

        view.Offset.ShouldBe(offset);
        changes.ShouldBe(0);
    }

    [Fact]
    public void PanBy_MovesThePictureByTheViewDelta()
    {
        var view = Centred();
        var before = view.ToView(new Vector2D(0, 0));

        view.PanBy(new Vector2D(10, -4));

        view.ToView(new Vector2D(0, 0)).ShouldBe(new Vector2D(before.X + 10, before.Y - 4));
    }

    [Fact]
    public void PanAndZoomWithinTheLimits_EachRaiseChangedOnce()
    {
        var view = Centred();
        var changes = 0;
        view.Changed += (_, _) => changes++;

        view.PanBy(new Vector2D(10, 10));
        changes.ShouldBe(1);

        view.ZoomAbout(_middle, 1.5);
        changes.ShouldBe(2);
    }

    [Fact]
    public void PanBy_StopsAtTheEdgeOfTheArea()
    {
        var view = Centred();

        view.PanBy(new Vector2D(100000, 100000));
        view.ToView(_area.Min).ShouldBe(new Vector2D(0, 0));

        view.PanBy(new Vector2D(-100000, -100000));
        view.ToView(_area.Max).ShouldBe(_screen.Max);
    }

    [Fact]
    public void PanBy_StopsAtTheSameCanvasEdgeAtEveryZoom()
    {
        var view = Centred();
        foreach (var factor in new[] { 1.0, 3, 0.1 })
        {
            view.ZoomAbout(_middle, factor);

            view.PanBy(new Vector2D(100000, 100000));

            view.ToCanvas(new Vector2D(0, 0)).X.ShouldBe(_area.Min.X, 1e-6);
        }
    }

    [Fact]
    public void Fit_CentresALargeCreationWithAMarginOnEverySide()
    {
        var view = new CanvasView(_area, () => new CanvasRect(new Vector2D(-1000, 0), new Vector2D(1000, 200))) { VisibleArea = _screen };

        view.Fit();

        var margin = _screen.Width * CanvasView.FitMargin;
        view.Zoom.ShouldBe((_screen.Width - (2 * margin)) / 2000, 1e-9);
        view.ToView(new Vector2D(-1000, 0)).X.ShouldBe(margin, 1e-9);
        view.ToView(new Vector2D(1000, 200)).X.ShouldBe(_screen.Width - margin, 1e-9);
        view.ToView(new Vector2D(0, 100)).X.ShouldBe(_middle.X, 1e-9);
        view.ToView(new Vector2D(0, 100)).Y.ShouldBe(_middle.Y, 1e-9);
    }

    [Fact]
    public void Fit_CentresASmallCreationWithoutMagnifyingIt()
    {
        var view = new CanvasView(_area, () => new CanvasRect(new Vector2D(10, 10), new Vector2D(50, 30))) { VisibleArea = _screen };

        view.Fit();

        view.Zoom.ShouldBe(1);
        view.ToView(new Vector2D(30, 20)).ShouldBe(_middle);
    }

    [Fact]
    public void Fit_NearTheEdge_StaysWithinTheArea()
    {
        var view = new CanvasView(_area, () => new CanvasRect(new Vector2D(1900, 900), new Vector2D(1950, 950))) { VisibleArea = _screen };

        view.Fit();

        view.ToView(_area.Max).ShouldBe(_screen.Max);
    }

    [Fact]
    public void Fit_WithNoContent_ShowsTheMiddleOfTheAreaAtOneToOne()
    {
        var view = Centred();
        view.ZoomAbout(new Vector2D(0, 0), 2);

        view.Fit();

        view.Zoom.ShouldBe(1);
        view.ToView(_area.Center).ShouldBe(_middle);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(2)]
    [InlineData(4)]
    public void UiScale_KeepsTheCreationsSizeOnScreen(double uiScale)
    {
        var view = new CanvasView(_area, () => new CanvasRect(new Vector2D(10, 10), new Vector2D(50, 30)))
        {
            UiScale = uiScale,
            VisibleArea = new CanvasRect(new Vector2D(0, 0), new Vector2D(1000 / uiScale, 500 / uiScale)),
        };

        view.Fit();
        view.Zoom.ShouldBe(1 / uiScale, 1e-9);

        view.ZoomAbout(view.VisibleArea!.Value.Center, 100);
        (view.Zoom * uiScale).ShouldBe(CanvasView.MaxZoom, 1e-9);
    }

    [Fact]
    public void UiScale_WhenItChanges_KeepsTheZoomOnScreen()
    {
        var view = Centred();
        view.ZoomAbout(_middle, 2);

        view.UiScale = 2;

        view.Zoom.ShouldBe(1);
    }

    [Fact]
    public void Fit_BeforeTheVisibleAreaIsKnown_DoesNothing()
    {
        var view = new CanvasView(_area, () => new CanvasRect(new Vector2D(10, 10), new Vector2D(50, 30)));

        view.Fit();

        view.Offset.ShouldBe(new Vector2D(0, 0));
    }

    [Fact]
    public void VisibleArea_WhenTheScreenGrows_KeepsTheViewInsideTheArea()
    {
        var view = Centred();
        view.ZoomAbout(new Vector2D(0, 0), 0.001);

        view.VisibleArea = new CanvasRect(new Vector2D(0, 0), new Vector2D(2000, 1000));

        view.Zoom.ShouldBe(0.5);
        view.ToView(_area.Min).ShouldBe(new Vector2D(0, 0));
    }

    [Fact]
    public void BuildArea_IsFilledByWholeGridCells()
    {
        var area = BuildViewModel.BuildArea;

        (area.Width % BuildViewModel.BuildGridStep).ShouldBe(0);
        (area.Height % BuildViewModel.BuildGridStep).ShouldBe(0);
    }

    [Fact]
    public void ZoomOutToShow_LeavesTheViewAloneWhenTheTargetIsShown()
    {
        var view = Centred();
        var (zoom, offset) = (view.Zoom, view.Offset);

        view.ZoomOutToShow(new CanvasRect(new Vector2D(-100, -50), new Vector2D(100, 50)));

        view.Zoom.ShouldBe(zoom);
        view.Offset.ShouldBe(offset);
    }

    [Fact]
    public void ZoomOutToShow_ZoomsOutJustEnoughToShowAWideTargetInsideTheMargin()
    {
        var view = Centred();
        var target = new CanvasRect(new Vector2D(0, 0), new Vector2D(1800, 100));

        view.ZoomOutToShow(target);

        view.Zoom.ShouldBe(_screen.Width * (1 - (2 * CanvasView.ShowMargin)) / target.Width, 1e-9);
        ShowsInsideTheMargin(view, target);
    }

    [Theory]
    // At 2×, with the canvas origin at the middle of the 1000-wide screen and a 50-unit margin.
    [InlineData(600, 650, -350)] // Off screen: its right edge comes to the margin.
    [InlineData(-100, 300, 350)] // Partly shown: it moves only as far as its hidden end needs.
    public void ZoomOutToShow_PansATargetThatFitsJustToTheMargin_NeverZoomingIn(double left, double right, double offsetX)
    {
        var view = Centred();
        view.ZoomAbout(_middle, 2);
        var target = new CanvasRect(new Vector2D(left, 0), new Vector2D(right, 20));

        view.ZoomOutToShow(target);

        view.Zoom.ShouldBe(2);
        view.Offset.ShouldBe(new Vector2D(offsetX, 250));
    }

    private static void ShowsInsideTheMargin(CanvasView view, CanvasRect target)
    {
        var margin = new Vector2D(_screen.Width * CanvasView.ShowMargin, _screen.Height * CanvasView.ShowMargin);
        foreach (var corner in new[] { view.ToView(target.Min), view.ToView(target.Max) })
        {
            corner.X.ShouldBeInRange(margin.X - 1e-9, _screen.Width - margin.X + 1e-9);
            corner.Y.ShouldBeInRange(margin.Y - 1e-9, _screen.Height - margin.Y + 1e-9);
        }
    }

    private static CanvasView Centred()
    {
        var view = new CanvasView(_area) { VisibleArea = _screen };
        view.Fit();
        return view;
    }
}
