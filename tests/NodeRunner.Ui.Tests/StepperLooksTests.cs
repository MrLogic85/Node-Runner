using NodeRunner.Creature;
using NodeRunner.Theme;

namespace NodeRunner.Ui.Tests;

/// <summary>How a Stepper's motion shows on its detent wheel (#1067, <c>docs/WORLD_VISUALS.md</c> → Stepper).</summary>
public sealed class StepperLooksTests
{
    private const int _steps = 8;
    private const double _spacing = 2 * Math.PI / _steps;
    private const double _frame = 1.0 / 60;

    [Fact]
    public void Holding_shows_the_pawl_seated_and_nothing_lit() =>
        Next(StepperLook.Hold, releasing: false, notch: 3, direction: 1, angleFromBuilt: 3 * _spacing).ShouldBe(StepperLook.Hold);

    // Mechanics count counter-clockwise; on screen that is a falling rotation, so stepping counter-clockwise sweeps negative.
    [Theory]
    [InlineData(1, 3, -1)]
    [InlineData(-1, 1, 1)]
    public void Releasing_lifts_the_pawl_lights_the_next_notch_and_sweeps_to_it(int direction, int lit, int sweepSign)
    {
        var look = Next(StepperLook.Hold, releasing: true, notch: 2, direction, angleFromBuilt: (2 * _spacing) + 0.1);

        look.Lifted.ShouldBeTrue();
        look.LitNotch.ShouldBe(lit);
        look.LitStrength.ShouldBe(1);
        look.Sweep.ShouldBe((float)((2 * _spacing) + 0.1 - (lit * _spacing)), 1e-5f);
        Math.Sign(look.Sweep).ShouldBe(sweepSign);
    }

    [Fact]
    public void The_sweep_counts_on_across_turns()
    {
        var look = Next(StepperLook.Hold, releasing: true, notch: _steps + 2, direction: 1, angleFromBuilt: (_steps + 2) * _spacing);

        look.LitNotch.ShouldBe(_steps + 3);
        look.Sweep.ShouldBe((float)-_spacing, 1e-5f);
    }

    [Fact]
    public void Once_caught_the_pawl_seats_and_the_notch_fades_out_over_the_fade_time()
    {
        var look = Next(StepperLook.Hold, releasing: true, notch: 2, direction: 1, angleFromBuilt: 2.5 * _spacing);
        var frames = 0;
        do
        {
            look = Next(look, releasing: false, notch: 3, direction: 1, angleFromBuilt: 3 * _spacing);
            frames++;
            if (look != StepperLook.Hold)
            {
                look.Lifted.ShouldBeFalse();
                look.Sweep.ShouldBe(0);
                look.LitNotch.ShouldBe(3);
            }
        }
        while (look != StepperLook.Hold && frames < 100);

        (frames * _frame).ShouldBe(StepperLooks.FadeTime, _frame);
    }

    // #1037: a release that ends short heads for its nearest notch; that pull is not a catch.
    [Theory]
    [InlineData(3, false)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public void A_step_that_ends_short_shows_as_hold_at_once(int notch, bool locked)
    {
        var look = Next(StepperLook.Hold, releasing: true, notch: 2, direction: 1, angleFromBuilt: 2.5 * _spacing);

        Next(look, releasing: false, notch, direction: 1, angleFromBuilt: 2.5 * _spacing, locked).ShouldBe(StepperLook.Hold);
    }

    [Fact]
    public void A_new_step_mid_fade_lights_its_notch_fully()
    {
        var look = Next(StepperLook.Hold, releasing: true, notch: 2, direction: 1, angleFromBuilt: 2.5 * _spacing);
        look = Next(look, releasing: false, notch: 3, direction: 1, angleFromBuilt: 3 * _spacing);
        look = Next(look, releasing: true, notch: 3, direction: 1, angleFromBuilt: 3 * _spacing);

        look.LitNotch.ShouldBe(4);
        look.LitStrength.ShouldBe(1);
        look.Lifted.ShouldBeTrue();
    }

    private static StepperLook Next(StepperLook previous, bool releasing, int notch, int direction, double angleFromBuilt, bool locked = true) =>
        StepperLooks.Next(previous, releasing, locked && !releasing, notch, direction, angleFromBuilt, _steps, _frame);
}
