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
    public void BeforeTheVisibleAreaIsKnown_TheViewIsOneToOne_AndFitAndEasingDoNothing()
    {
        var view = new CanvasView(_area, () => new CanvasRect(new Vector2D(10, 10), new Vector2D(50, 30)));

        view.Fit();
        view.ZoomOutToShow(new CanvasRect(new Vector2D(0, 0), new Vector2D(5000, 100)));
        view.Step(10);

        view.ToCanvas(new Vector2D(40, -20)).ShouldBe(new Vector2D(40, -20));
        view.ToView(new Vector2D(40, -20)).ShouldBe(new Vector2D(40, -20));
        view.IsEasing.ShouldBeFalse();
    }

    [Fact]
    public void ZoomAbout_KeepsThePointUnderTheFocus_BetweenTheWholeAreaAndMaxZoom_RaisingChangedOnlyWhenItMoves()
    {
        var view = Centred();
        var focus = new Vector2D(200, 120);
        var under = view.ToCanvas(focus);
        var changes = 0;
        view.Changed += (_, _) => changes++;

        view.ZoomAbout(focus, 2);
        var (zoom, canvas) = (view.Zoom, view.ToCanvas(focus));
        view.ZoomAbout(_middle, 100);
        var offset = view.Offset;
        view.ZoomAbout(new Vector2D(50, 50), 2);
        view.ZoomAbout(_middle, 0);

        (zoom, changes, view.Zoom, view.Offset).ShouldBe((2, 2, CanvasView.MaxZoom, offset));
        canvas.X.ShouldBe(under.X, 1e-9);
        canvas.Y.ShouldBe(under.Y, 1e-9);
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
    public void PanBy_MovesThePictureByTheViewDelta_AndStopsAtTheSameCanvasEdgeAtEveryZoom()
    {
        var view = Centred();
        var before = view.ToView(new Vector2D(0, 0));
        var changes = 0;
        view.Changed += (_, _) => changes++;

        view.PanBy(new Vector2D(10, -4));

        view.ToView(new Vector2D(0, 0)).ShouldBe(new Vector2D(before.X + 10, before.Y - 4));
        changes.ShouldBe(1);
        view.PanBy(new Vector2D(-100000, -100000));
        view.ToView(_area.Max).ShouldBe(_screen.Max);
        foreach (var factor in new[] { 1.0, 3, 0.1 })
        {
            view.ZoomAbout(_middle, factor);
            view.PanBy(new Vector2D(100000, 100000));
            view.ToCanvas(new Vector2D(0, 0)).X.ShouldBe(_area.Min.X, 1e-6);
            view.ToCanvas(new Vector2D(0, 0)).Y.ShouldBe(_area.Min.Y, 1e-6);
        }
    }

    // The large creation fits inside a fifth's margin on each side; the small one is not
    // magnified; the one near the edge stays within the area; with none, the middle shows at 1×.
    [Theory]
    [InlineData(true, -1000, 0, 1000, 200, 0.3, 0, 100)]
    [InlineData(true, 10, 10, 50, 30, 1, 30, 20)]
    [InlineData(true, 1900, 900, 1950, 950, 1, 1500, 750)]
    [InlineData(false, 0, 0, 0, 0, 1, 0, 0)]
    public void Fit_CentresTheCreation(bool hasContent, double left, double top, double right, double bottom, double zoom, double middleX, double middleY)
    {
        CanvasRect? content = hasContent ? new CanvasRect(new Vector2D(left, top), new Vector2D(right, bottom)) : null;
        var view = new CanvasView(_area, () => content) { VisibleArea = _screen };
        view.ZoomAbout(new Vector2D(0, 0), 2);

        view.Fit();

        view.Zoom.ShouldBe(zoom, 1e-9);
        view.ToCanvas(_middle).X.ShouldBe(middleX, 1e-9);
        view.ToCanvas(_middle).Y.ShouldBe(middleY, 1e-9);
    }

    [Theory]
    [InlineData(0.5)]
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
        view.UiScale = 0;

        view.Zoom.ShouldBe(1);
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

    // At 1×, centred, the screen shows x −500..500 and y −250..250. It grows only toward a side
    // the target reaches past, evenly where it reaches past both; nothing reached past the top or
    // bottom, so they grow evenly about the middle.
    [Theory]
    [InlineData(0, 1500, -500, 1500)]
    [InlineData(-1500, 0, -1500, 500)]
    [InlineData(-1000, 1000, -1000, 1000)]
    public void ZoomOutToShow_GrowsTheViewOnlyTowardTheSidesTheTargetReachesPast(double targetLeft, double targetRight, double shownLeft, double shownRight)
    {
        var view = Centred();

        view.ZoomOutToShow(new CanvasRect(new Vector2D(targetLeft, 0), new Vector2D(targetRight, 100)));
        view.Step(10);

        view.Zoom.ShouldBe(0.5, 1e-9);
        view.ToCanvas(_screen.Min).X.ShouldBe(shownLeft, 1e-6);
        view.ToCanvas(_screen.Max).X.ShouldBe(shownRight, 1e-6);
        view.ToCanvas(_middle).Y.ShouldBe(0, 1e-6);
        view.IsEasing.ShouldBeFalse();
    }

    [Fact]
    public void ZoomOutToShow_EasesThereKeepingTheStillEdgeInPlace_UntilTheTargetFitsOrThePlayerZooms()
    {
        var view = Centred();
        var growing = new CanvasRect(new Vector2D(0, 0), new Vector2D(1500, 100));

        view.ZoomOutToShow(growing);
        view.Step(1.0 / 60);

        view.IsEasing.ShouldBeTrue();
        view.Zoom.ShouldBe(Math.Pow(0.5, EaseShare(1.0 / 60)), 1e-9);
        view.ToCanvas(_screen.Min).X.ShouldBe(-500, 1e-6);
        var (zoom, offset) = (view.Zoom, view.Offset);
        view.ZoomOutToShow(new CanvasRect(new Vector2D(0, 0), new Vector2D(400, 100)));
        view.Step(10);
        (view.IsEasing, view.Zoom, view.Offset).ShouldBe((false, zoom, offset));
        view.ZoomOutToShow(growing);
        view.ZoomAbout(_middle, 1.1);
        view.IsEasing.ShouldBeFalse();
    }

    [Fact]
    public void ZoomOutToShow_MayGoPastTheBoundsIntoTheExtraReach_AndEasesBackWhenItGoes()
    {
        CanvasRect? extra = new CanvasRect(new Vector2D(0, 0), new Vector2D(5000, 100));
        var view = new CanvasView(_area, extraReach: () => extra) { VisibleArea = _screen };
        view.Fit();

        view.ZoomOutToShow(extra.Value);
        view.Step(10);

        view.ToCanvas(_screen.Max).X.ShouldBe(5000, 1e-6);
        extra = null;
        view.Step(10);

        view.Zoom.ShouldBe(view.MinZoom);
        view.ToCanvas(_screen.Max).X.ShouldBe(_area.Max.X, 1e-6);
        view.IsEasing.ShouldBeFalse();
    }

    private static double EaseShare(double deltaSeconds) => 1 - Math.Exp(-CanvasView.EaseRate * deltaSeconds);

    private static CanvasView Centred()
    {
        var view = new CanvasView(_area) { VisibleArea = _screen };
        view.Fit();
        return view;
    }
}
