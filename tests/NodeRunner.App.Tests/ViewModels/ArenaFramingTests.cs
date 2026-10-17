using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class ArenaFramingTests
{
    private const double _frame = 1.0 / 60;

    // The phone's arena at zoom 1, and the scene's ground.
    private const double _viewWidth = 565;
    private const double _viewHeight = 240;
    private const double _groundY = 396;

    [Theory]
    [InlineData(1200, 60)]
    [InlineData(80, 500)]
    public void Cut_FitsABigCreatureInsideTheMargins(double width, double height)
    {
        var creature = Standing(centreX: 0, width, height);

        var framing = Cut(creature);

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
    [InlineData(100, 60, 10_000)]
    public void ZoomToFit_ZoomsOutAsFarAsSizeAndSpeedNeed(double width, double height, double speedX)
    {
        var creature = Standing(0, width, height);
        var still = ArenaFraming.ZoomToFit(creature, 0, 0, _viewWidth, _viewHeight);

        var zoom = ArenaFraming.ZoomToFit(creature, speedX, 0, _viewWidth, _viewHeight);

        zoom.ShouldBeLessThan(0.25);
        (_viewWidth / zoom).ShouldBe((_viewWidth / still) + speedX, tolerance: 1e-6);
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
    public void Step_WidensPromptly_ThenNarrowsOnlyAfterTheDelayAndSlowly_TheGroundStaying()
    {
        var framing = Cut(Standing(0, 200, 60));
        var stretched = Standing(0, 1200, 60);
        var fit = ArenaFraming.ZoomToFit(stretched, 0, 0, _viewWidth, _viewHeight);
        var zooms = new List<double>();

        for (var frame = 0; frame < 22 * 60; frame++)
        {
            framing.Step(frame < 2 * 60 ? stretched : Standing(0, 200, 60), _groundY, _viewWidth, _viewHeight, _frame);
            zooms.Add(framing.Zoom);
            GroundFromTop(framing).ShouldBe(ArenaFraming.GroundFromTop, tolerance: 1e-9);
        }

        // Out within 0.75 s to a tenth of the way, in log space. Back in: held for 1.5 s, then a
        // second later 30-50% of the way.
        Math.Log(zooms[44] / fit).ShouldBeLessThan(Math.Log(1 / fit) * 0.1);
        var wide = zooms[(2 * 60) - 1];
        double ShareIn(double seconds) => 1 - (Math.Log(zooms[(2 * 60) + (int)(seconds * 60) - 1]) / Math.Log(wide));
        ShareIn(1.4).ShouldBe(0);
        ShareIn(1.6).ShouldBeGreaterThan(0);
        ShareIn(2.5).ShouldBeInRange(0.3, 0.5);
        zooms.Skip(2 * 60).ShouldBeInOrder(SortDirection.Ascending);
        zooms[^1].ShouldBe(1, tolerance: 0.01);
    }

    [Fact]
    public void Step_DoesNotPumpWithAStretchingGait()
    {
        const double gaitHertz = 1;
        var framing = Cut(Standing(0, 300, 60));
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

    // The last row is wider than the widest view leaves a second of travel room for.
    [Theory]
    [InlineData(3 * Metres.WorldUnitsPerMetre, 100)]
    [InlineData(-3 * Metres.WorldUnitsPerMetre, 1200)]
    [InlineData(30 * Metres.WorldUnitsPerMetre, 44_000)]
    public void Step_ZoomsOutForSpeedWithAThirdOfTheRoomBehind_AndBackAsTheCreatureStops(double speed, double width)
    {
        var fit = ArenaFraming.ZoomToFit(Standing(0, width, 60), 0, 0, _viewWidth, _viewHeight);
        var rest = _viewWidth / fit;
        var framing = Cut(Standing(0, width, 60));
        var centre = 0.0;

        for (var frame = 0; frame < 10 * 60; frame++)
        {
            centre += speed * _frame;
            framing.Step(Standing(centre, width, 60), _groundY, _viewWidth, _viewHeight, _frame);
            GroundFromTop(framing).ShouldBe(ArenaFraming.GroundFromTop, tolerance: 1e-9);
        }

        // The view shows what the creature needs plus one second of travel, and slides a sixth of
        // that ahead: the room behind is half the view at rest and a third of what speed added.
        var shown = _viewWidth / framing.Zoom;
        shown.ShouldBe(Math.Min(rest + Math.Abs(speed), ArenaFraming.MaxShownWidth), tolerance: 0.01 * rest);
        RoomBehind(framing, centre, speed, shown).ShouldBe((rest / 2) + ((shown - rest) / 3), tolerance: 0.005 * rest);

        for (var frame = 0; frame < 30 * 60; frame++)
        {
            framing.Step(Standing(centre, width, 60), _groundY, _viewWidth, _viewHeight, _frame);
        }

        FromLeft(framing, centre).ShouldBe(0.5, tolerance: 0.01);
        framing.Zoom.ShouldBe(fit, tolerance: 0.01 * fit);
    }

    [Fact]
    public void Step_CentresAHighJumpAndSettlesBack()
    {
        const double lift = 300;
        var framing = Cut(Standing(0, 100, 60));
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
    [InlineData(-200)]
    [InlineData(200)]
    public void Step_LeadsUpAndDownInTheAir(double speedY)
    {
        var bottom = _groundY - 5000;
        var framing = Cut(Standing(0, 100, 60, bottom));

        for (var frame = 0; frame < 10 * 60; frame++)
        {
            bottom += speedY * _frame;
            framing.Step(Standing(0, 100, 60, bottom), _groundY, _viewWidth, _viewHeight, _frame);
        }

        // Shown 240 + 200 tall: the room behind it, the way it came from, is the 120 it is at rest
        // and a third of the 200.
        var shown = _viewHeight / framing.Zoom;
        shown.ShouldBe(_viewHeight + Math.Abs(speedY), tolerance: 1);
        var centre = bottom - 30;
        var behind = speedY < 0
            ? framing.CameraY + (shown / 2) - centre
            : centre - (framing.CameraY - (shown / 2));
        behind.ShouldBe((_viewHeight / 2) + (Math.Abs(speedY) / 3), tolerance: 0.01 * _viewHeight);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(-40)]
    public void Step_HoldsAWalkerAtTheSafeZoneAsItSetsOffAndStopsAtOnce(double metresPerSecond)
    {
        // Off at full speed from a standstill: the view's eased speed lags, so its lead falls short.
        var speed = metresPerSecond * Metres.WorldUnitsPerMetre;
        var framing = Cut(Standing(0, 100, 60));
        var centre = 0.0;
        var closest = 1.0;
        for (var frame = 0; frame < 10 * 60; frame++)
        {
            centre += speed * _frame;
            var walker = Standing(centre, 100, 60);
            framing.Step(walker, _groundY, _viewWidth, _viewHeight, _frame);
            var front = speed > 0 ? 1 - FromLeft(framing, walker.Right) : FromLeft(framing, walker.Left);
            front.ShouldBeGreaterThanOrEqualTo(ArenaFraming.SafeMargin - 1e-9);
            closest = Math.Min(closest, front);
        }

        closest.ShouldBe(ArenaFraming.SafeMargin, tolerance: 1e-9);

        // Stopped dead, as against a wall: the eased speed still leads the view on for a while.
        var creature = Standing(centre, 100, 60);
        for (var frame = 0; frame < 6 * 60; frame++)
        {
            framing.Step(creature, _groundY, _viewWidth, _viewHeight, _frame);
            FromLeft(framing, creature.Left).ShouldBeGreaterThanOrEqualTo(ArenaFraming.SafeMargin);
            FromLeft(framing, creature.Right).ShouldBeLessThanOrEqualTo(1 - ArenaFraming.SafeMargin);
        }
    }

    [Fact]
    public void Step_DoesNotPanForAGaitStretchingNearTheSafeZone()
    {
        // At rest the box reaches 5% in from the right: inside SafeMargin, so the zone there is half that.
        var framing = Cut(new FramedCreature(0, 366, -100, 336, 370, _groundY));
        var cameraX = new List<double>();

        for (var frame = 0; frame < 10 * 60; frame++)
        {
            var reach = 335 + (35 * Math.Sin(Math.PI * 4 * frame * _frame));
            framing.Step(new FramedCreature(0, 366, -100, 336, reach, _groundY), _groundY, _viewWidth, _viewHeight, _frame);
            if (frame >= 3 * 60)
            {
                cameraX.Add(framing.CameraX);
            }
        }

        (cameraX.Max() - cameraX.Min()).ShouldBe(0, tolerance: 1e-9);
        FromLeft(framing, 0).ShouldBe(0.5, tolerance: 1e-9);
    }

    [Fact]
    public void Step_HoldsALaunchOfSixtyMetresAtTheSafeZoneAndLandsItOnTheGround()
    {
        // Off the ground at 40 m/s at once, up for a second and a half, and back as fast.
        const double speed = 40 * Metres.WorldUnitsPerMetre;
        var framing = Cut(Standing(0, 100, 60));
        var closest = 1.0;
        for (var frame = 0; frame < 8 * 60; frame++)
        {
            var seconds = (frame + 1) * _frame;
            var lift = speed * Math.Max(0, Math.Min(seconds, 3 - seconds));
            var creature = Standing(0, 100, 60, _groundY - lift);
            framing.Step(creature, _groundY, _viewWidth, _viewHeight, _frame);
            GroundFromTop(framing).ShouldBeGreaterThanOrEqualTo(ArenaFraming.GroundFromTop - 1e-9);
            FromTop(framing, creature.Top).ShouldBeGreaterThanOrEqualTo(ArenaFraming.SafeMargin - 1e-9);
            closest = Math.Min(closest, FromTop(framing, creature.Top));
        }

        closest.ShouldBe(ArenaFraming.SafeMargin, tolerance: 1e-9);
        GroundFromTop(framing).ShouldBe(ArenaFraming.GroundFromTop, tolerance: 1e-9);
    }

    [Fact]
    public void Step_CentresABoxTooWideForTheSafeZone()
    {
        var framing = Cut(Standing(0, 100, 60));

        // Spread wider than the view at once, the zoom still easing out to it, its centre of mass
        // well off the middle of its box.
        var creature = new FramedCreature(200, 366, -1000, 336, 600, _groundY);
        framing.Step(creature, _groundY, _viewWidth, _viewHeight, _frame);

        (FromLeft(framing, creature.Left) + FromLeft(framing, creature.Right)).ShouldBe(1, tolerance: 1e-9);
    }

    [Fact]
    public void Retarget_GlidesUpToACreatureInTheAirWithoutTakingTheJumpAsSpeed()
    {
        var high = Standing(0, 100, 60, bottom: _groundY - 400);
        var framing = Cut(Standing(0, 100, 60));
        var cameraY = new List<double> { framing.CameraY };

        framing.Retarget(high);
        for (var frame = 0; frame < 6 * 60; frame++)
        {
            framing.Step(high, _groundY, _viewWidth, _viewHeight, _frame);
            cameraY.Add(framing.CameraY);
            framing.Zoom.ShouldBe(1);
        }

        // It glides up, never a step of more than a fortieth of the way: the safe zone holds only once there.
        cameraY.ShouldBeInOrder(SortDirection.Descending);
        cameraY.Zip(cameraY.Skip(1), (a, b) => a - b).Max().ShouldBeLessThan(10);
        FromTop(framing, high.CentreY).ShouldBe(0.5, tolerance: 0.001);
    }

    [Fact]
    public void Retarget_GlidesToADistantCreatureThenHoldsItInTheSafeZone()
    {
        var far = Standing(3000, 100, 60);
        var framing = Cut(Standing(0, 100, 60));
        var cameraX = new List<double> { framing.CameraX };
        var entered = false;

        framing.Retarget(far);
        for (var frame = 0; frame < 8 * 60; frame++)
        {
            framing.Step(far, _groundY, _viewWidth, _viewHeight, _frame);
            cameraX.Add(framing.CameraX);
            var inside = FromLeft(framing, far.Left) >= ArenaFraming.SafeMargin - 1e-9
                && FromLeft(framing, far.Right) <= 1 - ArenaFraming.SafeMargin + 1e-9;
            entered.ShouldBeLessThanOrEqualTo(inside);
            entered = inside;
        }

        // It glides out and settles: a step of at most a tenth of the way, never back.
        cameraX.ShouldBeInOrder(SortDirection.Ascending);
        cameraX.Zip(cameraX.Skip(1), (a, b) => b - a).Max().ShouldBeLessThan(300);
        FromLeft(framing, far.CentreX).ShouldBe(0.5, tolerance: 0.001);
    }

    [Fact]
    public void Cut_RestartsTheZoomInDelayAndDropsTheGlide()
    {
        var wide = Standing(0, 1200, 60);
        var small = Standing(0, 200, 60);
        var framing = Cut(wide);
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

        framing.Zoom.ShouldBe(cutZoom, "1.4 s wanting to zoom in before the cut do not count after it.");
        framing.Cut(Standing(500, 100, 60), _groundY, _viewWidth, _viewHeight);
        framing.Zoom.ShouldBe(1);
        GroundFromTop(framing).ShouldBe(ArenaFraming.GroundFromTop, tolerance: 1e-9);
        FromLeft(framing, 500).ShouldBe(0.5, tolerance: 1e-9);
    }

    [Fact]
    public void Step_CutsOnTheFirstUsableCreature()
    {
        var framing = new ArenaFraming();

        framing.StepShelf(Standing(0, 100, 60), _viewWidth, _viewHeight, _frame);
        framing.Step(Standing(double.NaN, 100, 60), _groundY, _viewWidth, _viewHeight, _frame);
        framing.HasFrame.ShouldBeFalse();
        framing.Step(Standing(250, 1200, 60), _groundY, _viewWidth, _viewHeight, _frame);

        framing.HasFrame.ShouldBeTrue();
        framing.Zoom.ShouldBe(ArenaFraming.ZoomToFit(Standing(250, 1200, 60), 0, 0, _viewWidth, _viewHeight), tolerance: 1e-12);
    }

    [Theory]
    [InlineData(double.NaN, 0, 0, _viewWidth, _frame)]
    [InlineData(0, double.NaN, 0, _viewWidth, _frame)]
    [InlineData(0, 0, double.NegativeInfinity, _viewWidth, _frame)]
    [InlineData(0, 0, 0, 0, _frame)]
    [InlineData(0, 0, 0, _viewWidth, 0)]
    public void StepAndStepShelf_HoldTheFrameForAnUnusableCreatureViewOrTime(double centreX, double centreY, double top, double viewWidth, double deltaSeconds)
    {
        var framing = Cut(Standing(300, 100, 120));
        framing.Step(Standing(300, 100, 120), _groundY, _viewWidth, _viewHeight, _frame);
        framing.Shelf = 0.5;
        var held = (framing.CameraX, framing.CameraY, framing.Zoom);
        var creature = new FramedCreature(centreX, centreY, -50, top, 50, _groundY);

        framing.Step(creature, _groundY, viewWidth, _viewHeight, deltaSeconds);
        framing.StepShelf(creature, viewWidth, _viewHeight, deltaSeconds);

        (framing.CameraX, framing.CameraY, framing.Zoom).ShouldBe(held);
    }

    // A 60-tall creature's top sits 55% down a 240-tall view at zoom 1, a 120-tall one's 30%.
    [Theory]
    [InlineData(60, 0.3, 0, 0.8, 1)] // The shelf already fits.
    [InlineData(60, 0.55, 0, 0.85, 1)] // The ground lowers first,
    [InlineData(60, 0.65, 0, 0.95, 1)] // as far as the shelf's lowest ground.
    [InlineData(120, 0.5, 0, 0.95, 0.8)] // Then it zooms out only for what the ground cannot make,
    [InlineData(120, 0.75, 0, 0.95, 0.5)] // never shrinking the creature below the floor.
    [InlineData(120, 0.5, 0.1, 0.9, 0.7)] // The ground stops at the scale band,
    [InlineData(120, 0.5, 0.02, 0.95, 0.8)] // a thin one leaves the lowest ground,
    [InlineData(120, 0.5, 0.3, 0.8, 0.5)] // and a tall one the ground at rest.
    [InlineData(0, 0.5, 0, 0.8, 1)] // A flat creature has nothing to fit.
    public void FitShelf_LowersTheGroundFirstThenZoomsOut(double height, double shelf, double scaleBand, double expectedGround, double expectedZoom)
    {
        var (ground, zoom) = ArenaFraming.FitShelf(Standing(0, 100, height), 1, _viewHeight, shelf, scaleBand);

        ground.ShouldBe(expectedGround, tolerance: 1e-9);
        zoom.ShouldBe(expectedZoom, tolerance: 1e-9);
    }

    [Fact]
    public void ShelfAndScaleBand_SetBeforeTheFirstStepAfterACut_ArePartOfTheCut()
    {
        // As on a resize: the camera cuts to the new view, and only then are the callout's and the scale's shares of it known.
        var creature = Standing(0, 100, 120);
        var framing = Cut(creature);

        framing.Shelf = 0.5;
        var (ground, top) = (GroundFromTop(framing), FromTop(framing, creature.Top));
        framing.ScaleBand = 0.1;

        ground.ShouldBe(ArenaFraming.ShelfGroundFromTop, tolerance: 1e-9);
        top.ShouldBe(0.5 + ArenaFraming.Headroom, tolerance: 1e-9);
        GroundFromTop(framing).ShouldBe(0.9, tolerance: 1e-9);
    }

    [Theory]
    [InlineData(false, 0, ArenaFraming.ShelfGroundFromTop)]
    [InlineData(true, 0.1, 0.9)]
    public void Shelf_SetAfterAStep_OpensAtTheZoomOutRateNoLowerThanTheScaleBand(bool paused, double scaleBand, double lowestGround)
    {
        var creature = Standing(0, 100, 120);
        var framing = Cut(creature);
        Step(framing, creature, paused);

        framing.ScaleBand = scaleBand;
        framing.Shelf = 0.5;
        var grounds = new List<double> { GroundFromTop(framing) };
        for (var frame = 0; frame < 120; frame++)
        {
            Step(framing, creature, paused);
            grounds.Add(GroundFromTop(framing));
        }

        // Eased, not cut: a quarter second in, about 1 - e^-1 of the way at ZoomOutRate 4/s.
        grounds[0].ShouldBe(ArenaFraming.GroundFromTop, tolerance: 1e-9);
        ((grounds[15] - ArenaFraming.GroundFromTop) / (lowestGround - ArenaFraming.GroundFromTop)).ShouldBeInRange(0.5, 0.75);
        grounds.ShouldBeInOrder(SortDirection.Ascending);
        GroundFromTop(framing).ShouldBe(lowestGround, tolerance: 0.001);
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

        // The safe zone counts from the arena's top, not the shelf's, so it leaves this be.
        for (var frame = 0; frame < 120; frame++)
        {
            framing.Step(creature, _groundY, _viewWidth, _viewHeight, _frame);
        }

        GroundFromTop(framing).ShouldBe(ArenaFraming.ShelfGroundFromTop, tolerance: 1e-9);
        (GroundFromTop(framing) - FromTop(framing, creature.Top)).ShouldBe(ArenaFraming.CreatureFloor, tolerance: 1e-9);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shelf_ClosedIsHeldBeforeItIsGivenBack_AfterRestingSinceACut(bool paused)
    {
        var creature = Standing(0, 100, 120);
        var framing = new ArenaFraming { Shelf = 0.5 };
        framing.Cut(creature, _groundY, _viewWidth, _viewHeight);
        for (var frame = 0; frame < 240; frame++)
        {
            Step(framing, creature, paused);
        }

        var (y, zoom) = (framing.CameraY, framing.Zoom);
        zoom.ShouldBeLessThan(1);

        framing.Shelf = 0;
        for (var frame = 0; frame < 84; frame++)
        {
            Step(framing, creature, paused);
        }

        (framing.CameraY, framing.Zoom).ShouldBe((y, zoom));

        // A second after the hold ends, about 1 - e^-0.5 given back at ZoomInRate 0.5/s.
        for (var frame = 84; frame < 150; frame++)
        {
            Step(framing, creature, paused);
        }

        var givenBack = (ArenaFraming.ShelfGroundFromTop - GroundFromTop(framing)) / (ArenaFraming.ShelfGroundFromTop - ArenaFraming.GroundFromTop);
        givenBack.ShouldBeInRange(0.3, 0.5);
        for (var frame = 0; frame < 600; frame++)
        {
            Step(framing, creature, paused);
        }

        framing.Zoom.ShouldBe(1, tolerance: 0.01);
        GroundFromTop(framing).ShouldBe(ArenaFraming.GroundFromTop, tolerance: 0.01);
    }

    [Fact]
    public void Step_DoesNotPumpWhenTappingFromPartToPart()
    {
        var creature = Standing(0, 100, 120);
        var framing = Cut(creature);
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
        var framing = Cut(Standing(0, 100, 60));
        framing.StepShelf(Standing(0, 100, 60), _viewWidth, _viewHeight, _frame);
        var x = framing.CameraX;

        // Moved, but not out of the safe zone, which still holds while paused.
        var moved = Standing(100, 100, 60);

        framing.Shelf = 0.55;
        for (var frame = 0; frame < 120; frame++)
        {
            framing.StepShelf(moved, _viewWidth, _viewHeight, _frame);
        }

        framing.CameraX.ShouldBe(x);
        GroundFromTop(framing).ShouldBe(0.85, tolerance: 0.001);
    }

    [Fact]
    public void StepShelf_KeepsAWalkerHeldAtTheSafeZoneThereAsTheShelfClosesAndOnResuming()
    {
        // Paused just after setting off at 40 m/s, held at the zone's front edge.
        const double speed = 40 * Metres.WorldUnitsPerMetre;
        var framing = Cut(Standing(0, 100, 60));
        var centre = 0.0;
        for (var frame = 0; frame < 30; frame++)
        {
            centre += speed * _frame;
            framing.Step(Standing(centre, 100, 60), _groundY, _viewWidth, _viewHeight, _frame);
        }

        var creature = Standing(centre, 100, 60);
        (1 - FromLeft(framing, creature.Right)).ShouldBe(ArenaFraming.SafeMargin, tolerance: 1e-9);
        var cameraX = new List<double> { framing.CameraX };

        // A tall callout opens and closes again, then play resumes.
        framing.Shelf = 0.9;
        for (var frame = 0; frame < 20 * 60; frame++)
        {
            framing.Shelf = frame < 2 * 60 ? 0.9 : 0;
            framing.StepShelf(creature, _viewWidth, _viewHeight, _frame);
            cameraX.Add(framing.CameraX);
            FromLeft(framing, creature.Right).ShouldBeLessThanOrEqualTo(1 - ArenaFraming.SafeMargin + 1e-9);
        }

        centre += speed * _frame;
        framing.Step(Standing(centre, 100, 60), _groundY, _viewWidth, _viewHeight, _frame);
        cameraX.Add(framing.CameraX);

        cameraX.Zip(cameraX.Skip(1), (a, b) => Math.Abs(b - a)).Max().ShouldBeLessThan(2 * speed * _frame);
    }

    private static ArenaFraming Cut(FramedCreature creature)
    {
        var framing = new ArenaFraming();
        framing.Cut(creature, _groundY, _viewWidth, _viewHeight);
        return framing;
    }

    private static void Step(ArenaFraming framing, FramedCreature creature, bool paused)
    {
        if (paused)
        {
            framing.StepShelf(creature, _viewWidth, _viewHeight, _frame);
        }
        else
        {
            framing.Step(creature, _groundY, _viewWidth, _viewHeight, _frame);
        }
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
