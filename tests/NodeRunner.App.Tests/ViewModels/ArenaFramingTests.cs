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
    public void Cut_CentresASmallCreatureSidewaysOverTheGround()
    {
        var framing = new ArenaFraming();

        framing.Cut(Standing(centreX: 250, width: 100, height: 60), _groundY, _viewWidth, _viewHeight);

        framing.Zoom.ShouldBe(1);
        GroundFromTop(framing).ShouldBe(ArenaFraming.GroundFromTop, tolerance: 1e-9);
        FromLeft(framing, 250).ShouldBe(0.5, tolerance: 1e-9);
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
        FromLeft(framing, 0).ShouldBe(0.5, tolerance: 1e-9);
        FromLeft(framing, creature.Left).ShouldBeGreaterThanOrEqualTo(ArenaFraming.SideMargin - 1e-9);
        FromLeft(framing, creature.Right).ShouldBeLessThanOrEqualTo(1 - ArenaFraming.SideMargin + 1e-9);
        FromTop(framing, creature.Top).ShouldBeGreaterThanOrEqualTo(ArenaFraming.TopMargin + ArenaFraming.Headroom - 1e-9);
        GroundFromTop(framing).ShouldBeInRange(ArenaFraming.GroundFromTop - 1e-9, 1 - ArenaFraming.TopMargin + 1e-9);

        // No farther out than needed: the side or the standing height that binds touches its margin exactly.
        var leftSlack = FromLeft(framing, creature.Left) - ArenaFraming.SideMargin;
        var tallSlack = ArenaFraming.GroundFromTop - ArenaFraming.TopMargin - ArenaFraming.Headroom - (height * framing.Zoom / _viewHeight);
        Math.Min(leftSlack, tallSlack).ShouldBe(0, tolerance: 1e-9);
    }

    [Theory]
    [InlineData(1200, 60, 2000)]
    [InlineData(2400, 600, 0)]
    [InlineData(100, 60, 10_000)]
    public void ZoomToFit_ZoomsOutAsFarAsSizeAndSpeedNeed(double width, double height, double speedX)
    {
        var creature = Standing(0, width, height);
        var still = ArenaFraming.ZoomToFit(creature, 0, 0, _viewWidth, _viewHeight);

        var zoom = ArenaFraming.ZoomToFit(creature, speedX, 0, _viewWidth, _viewHeight);

        zoom.ShouldBeLessThan(0.25);
        (_viewWidth / zoom).ShouldBe((_viewWidth / still) + speedX, tolerance: 1e-6);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(-1000)]
    public void ZoomToFit_ZoomsOutForSpeedUpOrDown(double speedY)
    {
        var zoom = ArenaFraming.ZoomToFit(Standing(0, 100, 60), 0, speedY, _viewWidth, _viewHeight);

        (_viewHeight / zoom).ShouldBe(_viewHeight + Math.Abs(speedY), tolerance: 1e-6);
    }

    [Fact]
    public void ZoomToFit_KeepsACreatureCentredInTheAirInsideTheMargins()
    {
        // Its centre of mass near its top: centred, its bottom needs 0.4 of the height below the middle.
        var creature = Standing(0, 100, 200, centreY: _groundY - 180);

        var zoom = ArenaFraming.ZoomToFit(creature, 0, 0, _viewWidth, _viewHeight);

        zoom.ShouldBe((0.5 - ArenaFraming.TopMargin) * _viewHeight / 180, tolerance: 1e-9);
    }

    [Fact]
    public void ZoomToFit_ShowsNoMoreThanTheWidestViewForABlowUp()
    {
        var zoom = ArenaFraming.ZoomToFit(Standing(0, 1e12, 1e12), speedX: 1e12, speedY: 1e12, _viewWidth, _viewHeight);

        (_viewWidth / zoom).ShouldBe(ArenaFraming.MaxShownWidth, tolerance: 1e-6);
    }

    [Fact]
    public void Step_WidensPromptlyWhenTheCreatureStretches()
    {
        var framing = new ArenaFraming();
        framing.Cut(Standing(0, 200, 60), _groundY, _viewWidth, _viewHeight);
        var stretched = Standing(0, 1200, 60);
        var fit = ArenaFraming.ZoomToFit(stretched, 0, 0, _viewWidth, _viewHeight);

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

        var widest = ArenaFraming.ZoomToFit(Standing(0, 900, 60), 0, 0, _viewWidth, _viewHeight);
        settled.ShouldBeInOrder(SortDirection.Descending);
        settled[0].ShouldBe(widest, tolerance: widest * 0.1);
        settled[^1].ShouldBe(widest, tolerance: widest * 0.01);
    }

    // A 100-wide creature fits at zoom 1; a 1200-wide one at 565 × 0.45 / 600, either half binding.
    [Theory]
    [InlineData(3 * Metres.WorldUnitsPerMetre, 100, 1)]
    [InlineData(-3 * Metres.WorldUnitsPerMetre, 100, 1)]
    [InlineData(3 * Metres.WorldUnitsPerMetre, 1200, 0.42375)]
    [InlineData(-3 * Metres.WorldUnitsPerMetre, 1200, 0.42375)]
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
        FromLeft(framing, centre).ShouldBe(0.5, tolerance: 0.01);
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
    public void Step_DoesNotBobForAHoppingGait()
    {
        const double hopHertz = 2;
        const double hop = 40;
        var framing = new ArenaFraming();
        framing.Cut(Standing(0, 100, 60), _groundY, _viewWidth, _viewHeight);
        var settled = new List<double>();

        for (var frame = 0; frame < 10 * 60; frame++)
        {
            var lift = hop * Math.Abs(Math.Sin(Math.PI * hopHertz * frame * _frame));
            framing.Step(Standing(0, 100, 60, bottom: _groundY - lift), _groundY, _viewWidth, _viewHeight, _frame);
            GroundFromTop(framing).ShouldBe(ArenaFraming.GroundFromTop, tolerance: 1e-9);
            if (frame >= 3 * 60)
            {
                settled.Add(framing.Zoom);
            }
        }

        (settled.Max() / settled.Min()).ShouldBeLessThan(1.02);
    }

    [Fact]
    public void Step_CentresAHighJumpAndSettlesBack()
    {
        const double lift = 300;
        var framing = new ArenaFraming();
        framing.Cut(Standing(0, 100, 60), _groundY, _viewWidth, _viewHeight);
        var cameraY = new List<double>();

        // Up over half a second, held, and back down over half a second.
        for (var frame = 0; frame < 24 * 60; frame++)
        {
            var seconds = frame * _frame;
            var up = SmoothStep(seconds * 2) - SmoothStep((seconds - 8) * 2);
            var bottom = _groundY - (lift * up);
            framing.Step(Standing(0, 100, 60, bottom), _groundY, _viewWidth, _viewHeight, _frame);
            cameraY.Add(framing.CameraY);
            GroundFromTop(framing).ShouldBeGreaterThanOrEqualTo(ArenaFraming.GroundFromTop - 1e-9);
            if (frame == (8 * 60) - 1)
            {
                FromTop(framing, bottom - 30).ShouldBe(0.5, tolerance: 0.01);
            }
        }

        // It glides: never faster than the creature itself, at most 15 a frame.
        cameraY.Zip(cameraY.Skip(1), (a, b) => Math.Abs(b - a)).Max().ShouldBeLessThan(15);
        GroundFromTop(framing).ShouldBe(ArenaFraming.GroundFromTop, tolerance: 1e-9);
        framing.Zoom.ShouldBe(1, tolerance: 0.01);
    }

    [Theory]
    [InlineData(3 * Metres.WorldUnitsPerMetre)]
    [InlineData(-3 * Metres.WorldUnitsPerMetre)]
    public void Step_PutsTheRoomSpeedAddsInFrontOfAWalker(double speed)
    {
        var framing = new ArenaFraming();
        framing.Cut(Standing(0, 100, 60), _groundY, _viewWidth, _viewHeight);
        var centre = 0.0;

        for (var frame = 0; frame < 10 * 60; frame++)
        {
            centre += speed * _frame;
            framing.Step(Standing(centre, 100, 60), _groundY, _viewWidth, _viewHeight, _frame);
        }

        // Shown 565 + 300 wide: 150 of the added 300 slides the view ahead, and the room behind
        // stays the 282.5 it is at rest.
        var shown = _viewWidth / framing.Zoom;
        shown.ShouldBe(_viewWidth + Math.Abs(speed), tolerance: 1);
        RoomBehind(framing, centre, speed, shown).ShouldBe(_viewWidth / 2, tolerance: 0.01 * _viewWidth);
        GroundFromTop(framing).ShouldBe(ArenaFraming.GroundFromTop, tolerance: 1e-9);
    }

    [Fact]
    public void Step_KeepsTheRoomBehindAtTheWidestView()
    {
        // 44 000 wide fits in 48 889 at rest, so the widest view has 1 111 to spare for 3 000 a second.
        const double width = 44_000;
        const double speed = 3_000;
        var framing = new ArenaFraming();
        framing.Cut(Standing(0, width, 60), _groundY, _viewWidth, _viewHeight);
        var rest = _viewWidth / framing.Zoom;
        var centre = 0.0;

        for (var frame = 0; frame < 10 * 60; frame++)
        {
            centre += speed * _frame;
            framing.Step(Standing(centre, width, 60), _groundY, _viewWidth, _viewHeight, _frame);
        }

        var shown = _viewWidth / framing.Zoom;
        shown.ShouldBe(ArenaFraming.MaxShownWidth, tolerance: 1);
        RoomBehind(framing, centre, speed, shown).ShouldBe(rest / 2, tolerance: 0.01 * rest);
    }

    [Theory]
    [InlineData(-200)]
    [InlineData(200)]
    public void Step_LeadsUpAndDownInTheAir(double speedY)
    {
        var framing = new ArenaFraming();
        var bottom = _groundY - 5000;
        framing.Cut(Standing(0, 100, 60, bottom), _groundY, _viewWidth, _viewHeight);

        for (var frame = 0; frame < 10 * 60; frame++)
        {
            bottom += speedY * _frame;
            framing.Step(Standing(0, 100, 60, bottom), _groundY, _viewWidth, _viewHeight, _frame);
        }

        // Shown 240 + 200 tall: the room behind it, the way it came from, stays the 120 it is at rest.
        var shown = _viewHeight / framing.Zoom;
        shown.ShouldBe(_viewHeight + Math.Abs(speedY), tolerance: 1);
        var centre = bottom - 30;
        var behind = speedY < 0
            ? framing.CameraY + (shown / 2) - centre
            : centre - (framing.CameraY - (shown / 2));
        behind.ShouldBe(_viewHeight / 2, tolerance: 0.01 * _viewHeight);
    }

    [Fact]
    public void Step_StopsTheLeadIntoAFallAtTheGround()
    {
        const double speedY = 300;
        var framing = new ArenaFraming();
        var bottom = _groundY - 3000;
        framing.Cut(Standing(0, 100, 60, bottom), _groundY, _viewWidth, _viewHeight);

        while (bottom < _groundY)
        {
            bottom = Math.Min(_groundY, bottom + (speedY * _frame));
            framing.Step(Standing(0, 100, 60, bottom), _groundY, _viewWidth, _viewHeight, _frame);
            GroundFromTop(framing).ShouldBeGreaterThanOrEqualTo(ArenaFraming.GroundFromTop - 1e-9);
        }

        GroundFromTop(framing).ShouldBe(ArenaFraming.GroundFromTop, tolerance: 1e-9);
    }

    [Fact]
    public void Retarget_GlidesTheZoomToAnotherCreature()
    {
        var small = Standing(0, 100, 60);
        var big = Standing(400, 1200, 60);
        var framing = new ArenaFraming();
        framing.Cut(small, _groundY, _viewWidth, _viewHeight);
        var fit = ArenaFraming.ZoomToFit(big, 0, 0, _viewWidth, _viewHeight);
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
    public void Retarget_GlidesUpToACreatureInTheAirWithoutTakingTheJumpAsSpeed()
    {
        var high = Standing(0, 100, 60, bottom: _groundY - 400);
        var framing = new ArenaFraming();
        framing.Cut(Standing(0, 100, 60), _groundY, _viewWidth, _viewHeight);
        var cameraY = new List<double>();

        framing.Retarget(high);
        for (var frame = 0; frame < 6 * 60; frame++)
        {
            framing.Step(high, _groundY, _viewWidth, _viewHeight, _frame);
            cameraY.Add(framing.CameraY);
            framing.Zoom.ShouldBe(1);
        }

        cameraY.ShouldBeInOrder(SortDirection.Descending);
        FromTop(framing, high.CentreY).ShouldBe(0.5, tolerance: 0.001);
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
        GroundFromTop(framing).ShouldBe(ArenaFraming.GroundFromTop, tolerance: 1e-9);
        FromLeft(framing, 500).ShouldBe(0.5, tolerance: 1e-9);
    }

    [Fact]
    public void Step_CutsOnTheFirstUsableCreature()
    {
        var framing = new ArenaFraming();

        framing.Step(Standing(double.NaN, 100, 60), _groundY, _viewWidth, _viewHeight, _frame);
        framing.HasFrame.ShouldBeFalse();
        framing.Step(Standing(250, 1200, 60), _groundY, _viewWidth, _viewHeight, _frame);

        framing.HasFrame.ShouldBeTrue();
        framing.Zoom.ShouldBe(ArenaFraming.ZoomToFit(Standing(250, 1200, 60), 0, 0, _viewWidth, _viewHeight), tolerance: 1e-12);
    }

    [Theory]
    [InlineData(double.NaN, 0, 0)]
    [InlineData(double.PositiveInfinity, 0, 0)]
    [InlineData(0, double.NaN, 0)]
    [InlineData(0, 0, double.NegativeInfinity)]
    public void Step_HoldsTheFrameWhenTheCreatureIsNotUsable(double centreX, double centreY, double top)
    {
        var framing = new ArenaFraming();
        framing.Cut(Standing(0, 100, 60), _groundY, _viewWidth, _viewHeight);
        var (x, y, zoom) = (framing.CameraX, framing.CameraY, framing.Zoom);

        framing.Step(new FramedCreature(centreX, centreY, -50, top, 50, _groundY), _groundY, _viewWidth, _viewHeight, _frame);

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

    [Theory]
    [InlineData(0)]
    [InlineData(0.3)]
    [InlineData(0.5)]
    public void FitShelf_MovesNothingWhenTheShelfAlreadyFits(double shelf)
    {
        // 60 tall in a 240-tall view: its top sits 55% down, below any of these shelves.
        var (ground, zoom) = ArenaFraming.FitShelf(Standing(0, 100, 60), 1, _viewHeight, shelf);

        ground.ShouldBe(ArenaFraming.GroundFromTop);
        zoom.ShouldBe(ArenaFraming.MaxZoom);
    }

    [Theory]
    [InlineData(0.55, 0.85)]
    [InlineData(0.65, 0.95)]
    public void FitShelf_LowersTheGroundFirst(double shelf, double expectedGround)
    {
        var (ground, zoom) = ArenaFraming.FitShelf(Standing(0, 100, 60), 1, _viewHeight, shelf);

        ground.ShouldBe(expectedGround, tolerance: 1e-9);
        zoom.ShouldBe(ArenaFraming.MaxZoom);
    }

    [Fact]
    public void FitShelf_ZoomsOutOnlyForWhatTheGroundCannotMake()
    {
        var (ground, zoom) = ArenaFraming.FitShelf(Standing(0, 100, 120), 1, _viewHeight, 0.5);

        ground.ShouldBe(ArenaFraming.ShelfGroundFromTop);
        // The creature's 120 units fill the 40% between the shelf's headroom and the lowest ground.
        zoom.ShouldBe((ArenaFraming.ShelfGroundFromTop - 0.5 - ArenaFraming.Headroom) * _viewHeight / 120, tolerance: 1e-9);
    }

    [Fact]
    public void FitShelf_KeepsTheCreatureAtTheFloor()
    {
        var (ground, zoom) = ArenaFraming.FitShelf(Standing(0, 100, 120), 1, _viewHeight, 0.75);

        ground.ShouldBe(ArenaFraming.ShelfGroundFromTop);
        (120 * zoom / _viewHeight).ShouldBe(ArenaFraming.CreatureFloor, tolerance: 1e-9);
    }

    [Fact]
    public void Step_OpensTheShelfAtTheZoomOutRate()
    {
        var creature = Standing(0, 100, 120);
        var framing = new ArenaFraming();
        framing.Cut(creature, _groundY, _viewWidth, _viewHeight);
        framing.Step(creature, _groundY, _viewWidth, _viewHeight, _frame);

        framing.Shelf = 0.5;
        var grounds = new List<double>();
        for (var frame = 0; frame < 120; frame++)
        {
            framing.Step(creature, _groundY, _viewWidth, _viewHeight, _frame);
            grounds.Add(GroundFromTop(framing));
        }

        // A quarter second in, about 1 - e^-1 of the way at ZoomOutRate 4/s.
        var lowered = (grounds[14] - ArenaFraming.GroundFromTop) / (ArenaFraming.ShelfGroundFromTop - ArenaFraming.GroundFromTop);
        lowered.ShouldBeInRange(0.5, 0.75);
        grounds.ShouldBeInOrder(SortDirection.Ascending);
        GroundFromTop(framing).ShouldBe(ArenaFraming.ShelfGroundFromTop, tolerance: 0.001);
        FromTop(framing, creature.Top).ShouldBe(0.5 + ArenaFraming.Headroom, tolerance: 0.001);
        framing.Zoom.ShouldBeLessThan(1);
        FromLeft(framing, 0).ShouldBe(0.5, tolerance: 1e-9);
    }

    [Fact]
    public void Step_LeavesATooTallShelfCoveringTheCreatureAtTheFloor()
    {
        var creature = Standing(0, 100, 120);
        var framing = new ArenaFraming { Shelf = 0.75 };

        framing.Cut(creature, _groundY, _viewWidth, _viewHeight);

        GroundFromTop(framing).ShouldBe(ArenaFraming.ShelfGroundFromTop, tolerance: 1e-9);
        (GroundFromTop(framing) - FromTop(framing, creature.Top)).ShouldBe(ArenaFraming.CreatureFloor, tolerance: 1e-9);
        FromTop(framing, creature.Top).ShouldBeLessThan(0.75);
    }

    [Fact]
    public void Step_HoldsTheShelfBeforeGivingItBack()
    {
        var creature = Standing(0, 100, 120);
        var framing = new ArenaFraming { Shelf = 0.5 };
        framing.Cut(creature, _groundY, _viewWidth, _viewHeight);
        framing.Step(creature, _groundY, _viewWidth, _viewHeight, _frame);
        var (y, zoom) = (framing.CameraY, framing.Zoom);
        zoom.ShouldBeLessThan(1);

        framing.Shelf = 0;
        for (var frame = 0; frame < 84; frame++)
        {
            framing.Step(creature, _groundY, _viewWidth, _viewHeight, _frame);
        }

        (framing.CameraY, framing.Zoom).ShouldBe((y, zoom));

        // A second after the hold ends, about 1 - e^-0.5 given back at ZoomInRate 0.5/s.
        for (var frame = 84; frame < 150; frame++)
        {
            framing.Step(creature, _groundY, _viewWidth, _viewHeight, _frame);
        }

        var givenBack = (ArenaFraming.ShelfGroundFromTop - GroundFromTop(framing)) / (ArenaFraming.ShelfGroundFromTop - ArenaFraming.GroundFromTop);
        givenBack.ShouldBeInRange(0.3, 0.5);
        for (var frame = 0; frame < 600; frame++)
        {
            framing.Step(creature, _groundY, _viewWidth, _viewHeight, _frame);
        }

        framing.Zoom.ShouldBe(1, tolerance: 0.01);
        GroundFromTop(framing).ShouldBe(ArenaFraming.GroundFromTop, tolerance: 0.01);
    }

    [Fact]
    public void Step_HoldsAShelfThatHasRestedSinceACut()
    {
        var creature = Standing(0, 100, 120);
        var framing = new ArenaFraming { Shelf = 0.5 };
        framing.Cut(creature, _groundY, _viewWidth, _viewHeight);
        for (var frame = 0; frame < 240; frame++)
        {
            framing.StepShelf(creature, _viewWidth, _viewHeight, _frame);
        }

        var (y, zoom) = (framing.CameraY, framing.Zoom);

        framing.Shelf = 0;
        for (var frame = 0; frame < 84; frame++)
        {
            framing.StepShelf(creature, _viewWidth, _viewHeight, _frame);
        }

        (framing.CameraY, framing.Zoom).ShouldBe((y, zoom));
    }

    [Fact]
    public void Step_DoesNotPumpWhenTappingFromPartToPart()
    {
        var creature = Standing(0, 100, 120);
        var framing = new ArenaFraming();
        framing.Cut(creature, _groundY, _viewWidth, _viewHeight);
        framing.Step(creature, _groundY, _viewWidth, _viewHeight, _frame);
        var grounds = new List<double>();
        var zooms = new List<double>();

        foreach (var (shelf, frames) in new[] { (0.5, 18), (0.4, 18), (0.0, 60), (0.55, 30) })
        {
            framing.Shelf = shelf;
            for (var frame = 0; frame < frames; frame++)
            {
                framing.Step(creature, _groundY, _viewWidth, _viewHeight, _frame);
                grounds.Add(GroundFromTop(framing));
                zooms.Add(framing.Zoom);
            }
        }

        grounds.ShouldBeInOrder(SortDirection.Ascending);
        zooms.ShouldBeInOrder(SortDirection.Descending);
        zooms[^1].ShouldBeLessThan(zooms[0]);
    }

    [Fact]
    public void StepShelf_OpensTheShelfButFollowsNothingElse()
    {
        var framing = new ArenaFraming();
        framing.Cut(Standing(0, 100, 60), _groundY, _viewWidth, _viewHeight);
        framing.StepShelf(Standing(0, 100, 60), _viewWidth, _viewHeight, _frame);
        var x = framing.CameraX;
        var moved = Standing(300, 100, 60);

        framing.Shelf = 0.55;
        for (var frame = 0; frame < 120; frame++)
        {
            framing.StepShelf(moved, _viewWidth, _viewHeight, _frame);
        }

        framing.CameraX.ShouldBe(x);
        GroundFromTop(framing).ShouldBe(0.85, tolerance: 0.001);
    }

    [Fact]
    public void Shelf_SetBeforeTheFirstStepAfterACutIsPartOfTheCut()
    {
        // As on a resize: the camera cuts to the new view, and only then is the callout's share of it known.
        var creature = Standing(0, 100, 120);
        var framing = new ArenaFraming();
        framing.Cut(creature, _groundY, _viewWidth, _viewHeight);

        framing.Shelf = 0.5;

        GroundFromTop(framing).ShouldBe(ArenaFraming.ShelfGroundFromTop, tolerance: 1e-9);
        FromTop(framing, creature.Top).ShouldBe(0.5 + ArenaFraming.Headroom, tolerance: 1e-9);
    }

    [Fact]
    public void Shelf_SetAfterAStepEases()
    {
        var creature = Standing(0, 100, 120);
        var framing = new ArenaFraming();
        framing.Cut(creature, _groundY, _viewWidth, _viewHeight);
        framing.Step(creature, _groundY, _viewWidth, _viewHeight, _frame);

        framing.Shelf = 0.5;

        GroundFromTop(framing).ShouldBe(ArenaFraming.GroundFromTop, tolerance: 1e-9);
    }

    [Fact]
    public void Shelf_SetAfterAPausedStepEases()
    {
        var creature = Standing(0, 100, 120);
        var framing = new ArenaFraming();
        framing.Cut(creature, _groundY, _viewWidth, _viewHeight);
        framing.StepShelf(creature, _viewWidth, _viewHeight, _frame);

        framing.Shelf = 0.5;

        GroundFromTop(framing).ShouldBe(ArenaFraming.GroundFromTop, tolerance: 1e-9);
    }

    [Fact]
    public void StepShelf_DoesNothingBeforeTheFirstFrame()
    {
        var framing = new ArenaFraming { Shelf = 0.5 };

        framing.StepShelf(Standing(0, 100, 60), _viewWidth, _viewHeight, _frame);

        framing.HasFrame.ShouldBeFalse();
    }

    private static double SmoothStep(double t)
    {
        var x = Math.Clamp(t, 0, 1);
        return x * x * (3 - (2 * x));
    }

    // A box with its centre of mass in the middle, unless given.
    private static FramedCreature Standing(double centreX, double width, double height, double bottom = _groundY, double? centreY = null) =>
        new(centreX, centreY ?? (bottom - (height / 2)), centreX - (width / 2), bottom - height, centreX + (width / 2), bottom);

    // How much of the view lies behind a creature's centre, on the side it comes from.
    private static double RoomBehind(ArenaFraming framing, double centreX, double speed, double shown) =>
        speed > 0 ? centreX - (framing.CameraX - (shown / 2)) : framing.CameraX + (shown / 2) - centreX;

    // Where a world X shows across the view, measured from the left, as a share of its width.
    private static double FromLeft(ArenaFraming framing, double worldX) =>
        0.5 + ((worldX - framing.CameraX) * framing.Zoom / _viewWidth);

    // Where a world Y shows down the view, measured from the top, as a share of its height.
    private static double FromTop(ArenaFraming framing, double worldY) =>
        0.5 + ((worldY - framing.CameraY) * framing.Zoom / _viewHeight);

    private static double GroundFromTop(ArenaFraming framing) => FromTop(framing, _groundY);
}
