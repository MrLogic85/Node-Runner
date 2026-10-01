using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public class CanvasViewTests
{
    // A 4000×2000 area seen through a window that holds it at 0.25× with the edge margin around it.
    private const double _margin = CanvasView.EdgeMargin;
    private static readonly CanvasRect _area = new(new Vector2D(-2000, -1000), new Vector2D(2000, 1000));
    private static readonly CanvasRect _screen = new(new Vector2D(0, 0), new Vector2D(1000 + (2 * _margin), 500 + (2 * _margin)));
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
        view.ToView(_area.Min).ShouldBe(new Vector2D(_margin, _margin));
        view.ToView(_area.Max).ShouldBe(new Vector2D(1000 + _margin, 500 + _margin));
    }

    [Fact]
    public void MinZoom_ShowsTheWholeAreaWhenItsShapeDiffersFromTheScreen()
    {
        var view = new CanvasView(new CanvasRect(new Vector2D(-500, -500), new Vector2D(500, 500))) { VisibleArea = _screen };

        view.ZoomAbout(_middle, 0.001);

        view.Zoom.ShouldBe(0.5);
        view.ToView(new Vector2D(-500, -500)).ShouldBe(new Vector2D(250 + _margin, _margin));
        view.ToView(new Vector2D(500, 500)).ShouldBe(new Vector2D(750 + _margin, 500 + _margin));
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
        view.ToView(_area.Min).ShouldBe(new Vector2D(_margin, _margin));

        view.PanBy(new Vector2D(-100000, -100000));
        view.ToView(_area.Max).ShouldBe(new Vector2D(_screen.Max.X - _margin, _screen.Max.Y - _margin));
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

        view.ToView(_area.Max).ShouldBe(new Vector2D(_screen.Max.X - _margin, _screen.Max.Y - _margin));
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

        view.VisibleArea = new CanvasRect(new Vector2D(0, 0), new Vector2D(2000 + (2 * _margin), 1000 + (2 * _margin)));

        view.Zoom.ShouldBe(0.5);
        view.ToView(_area.Min).ShouldBe(new Vector2D(_margin, _margin));
    }

    [Fact]
    public void GridStep_DoublesAsTheViewZoomsOut()
    {
        var view = Centred();
        view.GridStep(48).ShouldBe(48);

        view.ZoomAbout(_middle, 0.5);
        view.GridStep(48).ShouldBe(48);

        view.ZoomAbout(_middle, 0.5);
        view.GridStep(48).ShouldBe(96);
    }

    [Fact]
    public void BuildArea_IsFilledByWholeGridCellsAtEveryStep()
    {
        var area = ConstructionViewModel.BuildArea;
        // A phone-sized slot at full zoom-out draws the coarsest grid.
        var view = new CanvasView(area) { VisibleArea = new CanvasRect(new Vector2D(0, 0), new Vector2D(300, 150)) };
        view.ZoomAbout(new Vector2D(150, 75), 0.001);
        var coarsest = view.GridStep(ConstructionViewModel.BuildGridStep);
        coarsest.ShouldBeGreaterThanOrEqualTo(8 * ConstructionViewModel.BuildGridStep);
        for (var step = ConstructionViewModel.BuildGridStep; step <= coarsest; step *= 2)
        {
            (area.Width % step).ShouldBe(0);
            (area.Height % step).ShouldBe(0);
        }
    }

    private static CanvasView Centred()
    {
        var view = new CanvasView(_area) { VisibleArea = _screen };
        view.Fit();
        return view;
    }
}
