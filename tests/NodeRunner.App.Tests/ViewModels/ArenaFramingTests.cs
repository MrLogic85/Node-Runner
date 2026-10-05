using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class ArenaFramingTests
{
    private const double _frame = 1.0 / 60;

    // The phone's arena at zoom 1, and the scene's ground.
    private const double _viewWidth = 565;
    private const double _viewHeight = 240;
    private const double _groundY = 396;

    [Fact]
    public void Cut_KeepsTodaysViewForASmallCreature()
    {
        var framing = new ArenaFraming();

        framing.Cut(Standing(centreX: 250, width: 100, height: 60), _groundY, _viewWidth, _viewHeight);

        framing.Zoom.ShouldBe(1);
        GroundFromTop(framing).ShouldBe(ArenaFraming.GroundFromTop, tolerance: 1e-9);
        FromLeft(framing, 250).ShouldBe(ArenaFollow.FocusFromLeft, tolerance: 1e-9);
    }

    [Theory]
    [InlineData(1200, 60)]
    [InlineData(80, 500)]
    [InlineData(900, 400)]
    public void Cut_FitsABigCreatureInsideTheMargins(double width, double height)
    {
        var creature = Standing(centreX: 0, width, height);
        var framing = new ArenaFraming();

        framing.Cut(creature, _groundY, _viewWidth, _viewHeight);

        framing.Zoom.ShouldBeLessThan(1);
        FromLeft(framing, 0).ShouldBe(ArenaFollow.FocusFromLeft, tolerance: 1e-9);
        FromLeft(framing, creature.Left).ShouldBeGreaterThanOrEqualTo(ArenaFraming.SideMargin - 1e-9);
        FromLeft(framing, creature.Right).ShouldBeLessThanOrEqualTo(1 - ArenaFraming.SideMargin + 1e-9);
        FromTop(framing, creature.Top).ShouldBeGreaterThanOrEqualTo(ArenaFraming.TopMargin + ArenaFraming.Headroom - 1e-9);
        GroundFromTop(framing).ShouldBe(ArenaFraming.GroundFromTop, tolerance: 1e-9);

        // No farther out than needed: the side or top that binds touches its margin exactly.
        var leftSlack = FromLeft(framing, creature.Left) - ArenaFraming.SideMargin;
        var topSlack = FromTop(framing, creature.Top) - ArenaFraming.TopMargin - ArenaFraming.Headroom;
        Math.Min(leftSlack, topSlack).ShouldBe(0, tolerance: 1e-9);
    }

    [Theory]
    [InlineData(1200, 60, 2000)]
    [InlineData(2400, 600, 0)]
    [InlineData(100, 60, 10_000)]
    public void ZoomToFit_ZoomsOutAsFarAsSizeAndSpeedNeed(double width, double height, double speedX)
    {
        var creature = Standing(0, width, height);
        var still = ArenaFraming.ZoomToFit(creature, 0, _viewWidth, _viewHeight);

        var zoom = ArenaFraming.ZoomToFit(creature, speedX, _viewWidth, _viewHeight);

        zoom.ShouldBeLessThan(0.25);
        (_viewWidth / zoom).ShouldBe((_viewWidth / still) + speedX, tolerance: 1e-6);
    }

    [Fact]
    public void ZoomToFit_ShowsNoMoreThanTheWidestViewForABlowUp()
    {
        var zoom = ArenaFraming.ZoomToFit(Standing(0, 1e12, 1e12), speedX: 1e12, _viewWidth, _viewHeight);

        (_viewWidth / zoom).ShouldBe(ArenaFraming.MaxShownWidth, tolerance: 1e-6);
    }

    [Fact]
    public void Step_WidensPromptlyWhenTheCreatureStretches()
    {
        var framing = new ArenaFraming();
        framing.Cut(Standing(0, 200, 60), _groundY, _viewWidth, _viewHeight);
        var stretched = Standing(0, 1200, 60);
        var fit = ArenaFraming.ZoomToFit(stretched, 0, _viewWidth, _viewHeight);

        for (var frame = 0; frame < 45; frame++)
        {
            framing.Step(stretched, _groundY, _viewWidth, _viewHeight, _frame);
        }

        Math.Log(framing.Zoom / fit).ShouldBeLessThan(Math.Log(1 / fit) * 0.1);
    }

    [Fact]
    public void Step_NarrowsOnlyAfterTheDelayAndThenSlowly()
    {
        var framing = new ArenaFraming();
        framing.Cut(Standing(0, 1200, 60), _groundY, _viewWidth, _viewHeight);
        var wide = framing.Zoom;
        var zooms = new List<double>();

        for (var frame = 0; frame < 20 * 60; frame++)
        {
            framing.Step(Standing(0, 200, 60), _groundY, _viewWidth, _viewHeight, _frame);
            zooms.Add(framing.Zoom);
        }

        // Held for 1.5 s, then a second later 30-50% of the way in, measured in log space.
        double ShareIn(double seconds) => 1 - (Math.Log(zooms[(int)(seconds * 60) - 1]) / Math.Log(wide));
        ShareIn(1.4).ShouldBe(0);
        ShareIn(1.6).ShouldBeGreaterThan(0);
        ShareIn(2.5).ShouldBeInRange(0.3, 0.5);
        zooms.ShouldBeInOrder(SortDirection.Ascending);
        zooms[^1].ShouldBe(1, tolerance: 0.01);
    }

    [Fact]
    public void Step_DoesNotPumpWithAStretchingGait()
    {
        const double gaitHertz = 1;
        var framing = new ArenaFraming();
        framing.Cut(Standing(0, 300, 60), _groundY, _viewWidth, _viewHeight);
        var settled = new List<double>();

        for (var frame = 0; frame < 10 * 60; frame++)
        {
            var stretch = 0.5 + (0.5 * Math.Sin(2 * Math.PI * gaitHertz * frame * _frame));
            framing.Step(Standing(0, 300 + (600 * stretch), 60), _groundY, _viewWidth, _viewHeight, _frame);
            if (frame >= 3 * 60)
            {
                settled.Add(framing.Zoom);
            }
        }

        var widest = ArenaFraming.ZoomToFit(Standing(0, 900, 60), 0, _viewWidth, _viewHeight);
        settled.ShouldBeInOrder(SortDirection.Descending);
        settled[0].ShouldBe(widest, tolerance: widest * 0.1);
        settled[^1].ShouldBe(widest, tolerance: widest * 0.01);
    }

    // A 100-wide creature fits at zoom 1; a 1200-wide one at 565 × 0.38 / 600, its left half binding.
    [Theory]
    [InlineData(3 * Metres.WorldUnitsPerMetre, 100, 1)]
    [InlineData(-3 * Metres.WorldUnitsPerMetre, 100, 1)]
    [InlineData(3 * Metres.WorldUnitsPerMetre, 1200, 0.35783333333333334)]
    [InlineData(-3 * Metres.WorldUnitsPerMetre, 1200, 0.35783333333333334)]
    public void Step_ZoomsOutWithSpeedAndBackAsTheCreatureStops(double speed, double width, double fit)
    {
        var framing = new ArenaFraming();
        framing.Cut(Standing(0, width, 60), _groundY, _viewWidth, _viewHeight);
        var centre = 0.0;

        for (var frame = 0; frame < 10 * 60; frame++)
        {
            centre += speed * _frame;
            framing.Step(Standing(centre, width, 60), _groundY, _viewWidth, _viewHeight, _frame);
        }

        var running = framing.Zoom;
        for (var frame = 0; frame < 30 * 60; frame++)
        {
            framing.Step(Standing(centre, width, 60), _groundY, _viewWidth, _viewHeight, _frame);
        }

        // The view shows what the creature needs plus one second of travel.
        var shownWhileRunning = (_viewWidth / fit) + Math.Abs(speed);
        running.ShouldBe(_viewWidth / shownWhileRunning, tolerance: 0.02 * fit);
        FromLeft(framing, centre).ShouldBe(ArenaFollow.FocusFromLeft, tolerance: 0.01);
        framing.Zoom.ShouldBe(fit, tolerance: 0.01 * fit);
    }

    [Fact]
    public void Step_KeepsTheGroundAtItsHeightOnScreenWhileZooming()
    {
        var framing = new ArenaFraming();
        framing.Cut(Standing(0, 200, 60), _groundY, _viewWidth, _viewHeight);
        var zooms = new HashSet<double>();

        for (var frame = 0; frame < 6 * 60; frame++)
        {
            var width = frame < 2 * 60 ? 1200 : 200;
            framing.Step(Standing(0, width, 60), _groundY, _viewWidth, _viewHeight, _frame);
            zooms.Add(framing.Zoom);
            GroundFromTop(framing).ShouldBe(ArenaFraming.GroundFromTop, tolerance: 1e-9);
        }

        zooms.Count.ShouldBeGreaterThan(60);
    }

    [Fact]
    public void Step_DoesNotRiseForAJumpInsideTheTopMargin()
    {
        const double height = 500;
        var framing = new ArenaFraming();
        framing.Cut(Standing(0, 100, height), _groundY, _viewWidth, _viewHeight);
        var cameraY = framing.CameraY;
        var room = (ArenaFraming.GroundFromTop - ArenaFraming.TopMargin) * _viewHeight / framing.Zoom - height;

        room.ShouldBe(ArenaFraming.Headroom * _viewHeight / framing.Zoom, tolerance: 1e-9);

        for (var frame = 0; frame < 120; frame++)
        {
            var lift = room * Math.Sin(Math.PI * frame / 120.0);
            framing.Step(Standing(0, 100, height, bottom: _groundY - lift), _groundY, _viewWidth, _viewHeight, _frame);
            framing.CameraY.ShouldBe(cameraY);
        }
    }

    [Fact]
    public void Step_RisesSmoothlyAfterAHighJumpAndSettlesBack()
    {
        const double height = 60;
        const double lift = 300;
        var framing = new ArenaFraming();
        framing.Cut(Standing(0, 100, height), _groundY, _viewWidth, _viewHeight);
        var groundLine = new List<double>();

        for (var frame = 0; frame < 4 * 60; frame++)
        {
            var bottom = frame < 2 * 60 ? _groundY - lift : _groundY;
            framing.Step(Standing(0, 100, height, bottom), _groundY, _viewWidth, _viewHeight, _frame);
            groundLine.Add(framing.GroundLineY);
            if (frame == (2 * 60) - 1)
            {
                FromTop(framing, bottom - height).ShouldBe(ArenaFraming.TopMargin, tolerance: 0.01);
            }
        }

        // The top must sit on the margin: ground line 396 - 360 + 0.7 × 240 = 204, a 192 rise.
        // A quarter second in, the camera has eased only part of the way.
        const double fullRise = 192;
        var shareRisen = (_groundY - groundLine[(60 / 4) - 1]) / fullRise;
        shareRisen.ShouldBeInRange(0.3, 0.7);
        (_groundY - groundLine.Min()).ShouldBe(fullRise, tolerance: 1);
        groundLine[^1].ShouldBe(_groundY, tolerance: 1);
    }

    [Fact]
    public void Retarget_GlidesTheZoomToAnotherCreature()
    {
        var small = Standing(0, 100, 60);
        var big = Standing(400, 1200, 60);
        var framing = new ArenaFraming();
        framing.Cut(small, _groundY, _viewWidth, _viewHeight);
        var fit = ArenaFraming.ZoomToFit(big, 0, _viewWidth, _viewHeight);
        var zooms = new List<double>();

        framing.Retarget(big);
        for (var frame = 0; frame < 120; frame++)
        {
            framing.Step(big, _groundY, _viewWidth, _viewHeight, _frame);
            zooms.Add(framing.Zoom);
        }

        zooms[0].ShouldBeGreaterThan(fit);
        zooms[0].ShouldBeLessThan(1);
        zooms.ShouldBeInOrder(SortDirection.Descending);
        zooms.Min().ShouldBe(fit, tolerance: fit * 0.01);
    }

    [Fact]
    public void Cut_RestartsTheZoomInDelay()
    {
        var wide = Standing(0, 1200, 60);
        var small = Standing(0, 200, 60);
        var framing = new ArenaFraming();
        framing.Cut(wide, _groundY, _viewWidth, _viewHeight);
        for (var frame = 0; frame < 84; frame++)
        {
            framing.Step(small, _groundY, _viewWidth, _viewHeight, _frame);
        }

        framing.Cut(wide, _groundY, _viewWidth, _viewHeight);
        var cutZoom = framing.Zoom;
        for (var frame = 0; frame < 12; frame++)
        {
            framing.Step(small, _groundY, _viewWidth, _viewHeight, _frame);
        }

        framing.Zoom.ShouldBe(cutZoom);
    }

    [Fact]
    public void Cut_DropsTheGlide()
    {
        var framing = new ArenaFraming();
        framing.Cut(Standing(0, 1200, 60, bottom: _groundY - 400), _groundY, _viewWidth, _viewHeight);
        var small = Standing(500, 100, 60);

        framing.Cut(small, _groundY, _viewWidth, _viewHeight);

        framing.Zoom.ShouldBe(1);
        framing.GroundLineY.ShouldBe(_groundY);
        FromLeft(framing, 500).ShouldBe(ArenaFollow.FocusFromLeft, tolerance: 1e-9);
    }

    [Fact]
    public void Step_CutsOnTheFirstUsableCreature()
    {
        var framing = new ArenaFraming();

        framing.Step(Standing(double.NaN, 100, 60), _groundY, _viewWidth, _viewHeight, _frame);
        framing.HasFrame.ShouldBeFalse();
        framing.Step(Standing(250, 1200, 60), _groundY, _viewWidth, _viewHeight, _frame);

        framing.HasFrame.ShouldBeTrue();
        framing.Zoom.ShouldBe(ArenaFraming.ZoomToFit(Standing(250, 1200, 60), 0, _viewWidth, _viewHeight), tolerance: 1e-12);
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 0)]
    [InlineData(0, double.NegativeInfinity)]
    public void Step_HoldsTheFrameWhenTheCreatureIsNotUsable(double centreX, double top)
    {
        var framing = new ArenaFraming();
        framing.Cut(Standing(0, 100, 60), _groundY, _viewWidth, _viewHeight);
        var (x, y, zoom) = (framing.CameraX, framing.CameraY, framing.Zoom);

        framing.Step(new FramedCreature(centreX, -50, top, 50, _groundY), _groundY, _viewWidth, _viewHeight, _frame);

        (framing.CameraX, framing.CameraY, framing.Zoom).ShouldBe((x, y, zoom));
    }

    [Fact]
    public void Step_HoldsTheFrameForAnEmptyView()
    {
        var framing = new ArenaFraming();
        framing.Cut(Standing(0, 100, 60), _groundY, _viewWidth, _viewHeight);
        var (x, y, zoom) = (framing.CameraX, framing.CameraY, framing.Zoom);

        framing.Step(Standing(300, 1200, 60), _groundY, 0, _viewHeight, _frame);

        (framing.CameraX, framing.CameraY, framing.Zoom).ShouldBe((x, y, zoom));
    }

    private static FramedCreature Standing(double centreX, double width, double height, double bottom = _groundY) =>
        new(centreX, centreX - (width / 2), bottom - height, centreX + (width / 2), bottom);

    // Where a world X shows across the view, measured from the left, as a share of its width.
    private static double FromLeft(ArenaFraming framing, double worldX) =>
        0.5 + ((worldX - framing.CameraX) * framing.Zoom / _viewWidth);

    // Where a world Y shows down the view, measured from the top, as a share of its height.
    private static double FromTop(ArenaFraming framing, double worldY) =>
        0.5 + ((worldY - framing.CameraY) * framing.Zoom / _viewHeight);

    private static double GroundFromTop(ArenaFraming framing) => FromTop(framing, _groundY);
}
