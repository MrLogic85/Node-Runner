using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class ArenaFollowTests
{
    private const double _frame = 1.0 / 60;

    [Fact]
    public void Step_SnapsToTheFirstFiniteCentre()
    {
        var follow = new ArenaFollow();

        follow.Step(double.NaN, _frame);
        follow.HasFocus.ShouldBeFalse();
        follow.Step(250, _frame);

        follow.HasFocus.ShouldBeTrue();
        follow.Focus.ShouldBe(250);
    }

    [Fact]
    public void Step_GlidesToANewTargetWithoutJumping_WhateverTheFrameRate()
    {
        var follow = new ArenaFollow();
        var at30 = new ArenaFollow();
        follow.SnapTo(0);
        at30.SnapTo(0);
        var positions = new List<double>();

        for (var frame = 0; frame < 180; frame++)
        {
            follow.Step(1000, _frame);
            positions.Add(follow.Focus);
            if (frame % 2 == 0)
            {
                at30.Step(1000, 2 * _frame);
            }
        }

        positions[0].ShouldBeLessThan(1000 * ArenaFollow.EaseRate * _frame * 1.01);
        positions.ShouldBeInOrder(SortDirection.Ascending);
        positions[^1].ShouldBe(1000, tolerance: 1);
        at30.Focus.ShouldBe(follow.Focus, tolerance: 1e-9);
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
                settled.Add(follow.Shown);
            }
        }

        (settled.Max() - settled.Min()).ShouldBeLessThan(amplitude * 2 * 0.3);
    }

    [Fact]
    public void Camera_KeepsAFastCreatureOnTheShownPoint()
    {
        const double speed = 10 * Metres.WorldUnitsPerMetre;
        var follow = new ArenaFollow();
        follow.SnapTo(0);
        var centre = 0.0;

        for (var frame = 0; frame < 600; frame++)
        {
            centre += speed * _frame;
            follow.Step(centre, _frame);
        }

        // Within 5% of a 460-wide view of where it shows.
        Math.Abs(centre - follow.Shown).ShouldBeLessThan(0.05 * 460);
    }

    [Theory]
    [InlineData(400, 1.0 / 60)]
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
                toward.Add((centre - follow.Shown) * Math.Sign(gap));
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

        follow.Speed.ShouldBe(0);
        follow.Aim.ShouldBe(-50);
        follow.Shown.ShouldBe(-50);
    }

    [Fact]
    public void StepAndRetarget_IgnoreANonFiniteCentreOrNoTime()
    {
        var follow = new ArenaFollow();
        follow.SnapTo(120);

        follow.SnapTo(double.PositiveInfinity);
        follow.Retarget(double.NaN);
        follow.Step(double.NaN, _frame);
        follow.Step(500, 0);

        (follow.Focus, follow.Shown, follow.Speed).ShouldBe((120, 120, 0));
    }
}
