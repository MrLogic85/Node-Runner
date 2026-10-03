using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class ArenaFollowTests
{
    private const double _frame = 1.0 / 60;

    [Fact]
    public void Step_SnapsToTheFirstCentre()
    {
        var follow = new ArenaFollow();

        follow.Step(250, _frame);

        follow.HasFocus.ShouldBeTrue();
        follow.FocusX.ShouldBe(250);
    }

    [Fact]
    public void Step_GlidesToANewTargetWithoutJumping()
    {
        var follow = new ArenaFollow();
        follow.SnapTo(0);
        var positions = new List<double>();

        for (var frame = 0; frame < 180; frame++)
        {
            follow.Step(1000, _frame);
            positions.Add(follow.FocusX);
        }

        positions[0].ShouldBeLessThan(1000 * ArenaFollow.EaseRate * _frame * 1.01);
        positions.ShouldBeInOrder(SortDirection.Ascending);
        positions[^1].ShouldBe(1000, tolerance: 1);
    }

    [Fact]
    public void Camera_DampsAWobblingCentre()
    {
        const double amplitude = 20;
        const double wobbleHertz = 2;
        var follow = new ArenaFollow();
        follow.SnapTo(0);
        var settled = new List<double>();

        for (var frame = 0; frame < 600; frame++)
        {
            var centre = amplitude * Math.Sin(2 * Math.PI * wobbleHertz * frame * _frame);
            follow.Step(centre, _frame);
            if (frame >= 300)
            {
                settled.Add(follow.ShownX);
            }
        }

        (settled.Max() - settled.Min()).ShouldBeLessThan(amplitude * 2 * 0.3);
    }

    [Fact]
    public void Camera_KeepsAFastCreatureAtTheFocus()
    {
        const double speed = 10 * Metres.WorldUnitsPerMetre;
        const double viewWidth = 460;
        var follow = new ArenaFollow();
        follow.SnapTo(0);
        var centre = 0.0;

        for (var frame = 0; frame < 600; frame++)
        {
            centre += speed * _frame;
            follow.Step(centre, _frame);
        }

        var fromLeft = ArenaFollow.FocusFromLeft + ((centre - follow.ShownX) / viewWidth);
        fromLeft.ShouldBeInRange(0.38, 0.48);
    }

    [Theory]
    [InlineData(150, 1.0 / 60)]
    [InlineData(400, 1.0 / 60)]
    [InlineData(600, 4.0 / 60)]
    [InlineData(-300, 4.0 / 60)]
    public void Camera_GlidesToAnotherMovingCreatureWithoutOvershooting(double gap, double frameSeconds)
    {
        const double speed = 2 * Metres.WorldUnitsPerMetre;
        var switchFrame = (int)(5 / frameSeconds);
        var follow = new ArenaFollow();
        follow.SnapTo(0);
        var centre = 0.0;
        var toward = new List<double>();

        for (var frame = 0; frame < switchFrame * 3; frame++)
        {
            centre += speed * frameSeconds;
            if (frame == switchFrame)
            {
                centre += gap;
                follow.Retarget(centre);
            }

            follow.Step(centre, frameSeconds);
            if (frame >= switchFrame)
            {
                toward.Add((centre - follow.ShownX) * Math.Sign(gap));
            }
        }

        toward[0].ShouldBeGreaterThan(Math.Abs(gap) * 0.9);
        toward.Min().ShouldBeGreaterThan(toward[^1] - 1);
        Math.Abs(toward[^1]).ShouldBeLessThan(speed * 0.05);
    }

    [Fact]
    public void SnapTo_StartsAtRest()
    {
        var follow = new ArenaFollow();
        follow.SnapTo(0);
        for (var frame = 0; frame < 120; frame++)
        {
            follow.Step(frame * 10, _frame);
        }

        follow.SnapTo(-50);

        follow.SpeedX.ShouldBe(0);
        follow.AimX.ShouldBe(-50);
        follow.ShownX.ShouldBe(-50);
    }

    [Fact]
    public void Step_IsTheSameWhateverTheFrameRate()
    {
        var at30 = new ArenaFollow();
        var at120 = new ArenaFollow();
        at30.SnapTo(0);
        at120.SnapTo(0);

        for (var frame = 0; frame < 30; frame++)
        {
            at30.Step(500, 1.0 / 30);
        }

        for (var frame = 0; frame < 120; frame++)
        {
            at120.Step(500, 1.0 / 120);
        }

        at120.FocusX.ShouldBe(at30.FocusX, tolerance: 1e-9);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Step_HoldsTheFocusWhenTheCentreIsNotFinite(double centre)
    {
        var follow = new ArenaFollow();
        follow.SnapTo(120);

        follow.Step(centre, _frame);

        follow.FocusX.ShouldBe(120);
    }

    [Fact]
    public void Step_WaitsForAFiniteFirstCentre()
    {
        var follow = new ArenaFollow();

        follow.Step(double.NaN, _frame);

        follow.HasFocus.ShouldBeFalse();
    }

    [Fact]
    public void CameraX_PutsTheFocusAtItsShareOfTheView()
    {
        const double viewWidth = 1000;

        var cameraX = ArenaFollow.CameraX(400, viewWidth);

        (cameraX - (viewWidth / 2)).ShouldBe(400 - (ArenaFollow.FocusFromLeft * viewWidth), tolerance: 1e-9);
    }
}
