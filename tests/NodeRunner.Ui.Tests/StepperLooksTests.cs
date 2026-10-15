using NodeRunner.Creature;
using NodeRunner.Theme;

namespace NodeRunner.Ui.Tests;

/// <summary>How a Stepper's motion shows on its detent wheel (#1067, <c>docs/WORLD_VISUALS.md</c> → Stepper).</summary>
public sealed class StepperLooksTests
{
    private const int _steps = 8;
    private const double _spacing = 2 * Math.PI / _steps;

    [Fact]
    public void Holding_shows_the_pawl_seated() =>
        StepperLooks.Next(releasing: false, notch: 3, direction: 1, angleFromBuilt: 3 * _spacing, _steps).ShouldBe(StepperLook.Hold);

    // Mechanics count counter-clockwise; on screen that is a falling rotation, so stepping counter-clockwise sweeps negative.
    [Theory]
    [InlineData(1, 3, -1)]
    [InlineData(-1, 1, 1)]
    public void Releasing_lifts_the_pawl_and_sweeps_to_the_next_notch(int direction, int target, int sweepSign)
    {
        var look = StepperLooks.Next(releasing: true, notch: 2, direction, angleFromBuilt: (2 * _spacing) + 0.1, _steps);

        look.Lifted.ShouldBeTrue();
        look.Sweep.ShouldBe((float)((2 * _spacing) + 0.1 - (target * _spacing)), 1e-5f);
        Math.Sign(look.Sweep).ShouldBe(sweepSign);
    }

    [Fact]
    public void The_sweep_counts_on_across_turns() =>
        StepperLooks.Next(releasing: true, notch: _steps + 2, direction: 1, angleFromBuilt: (_steps + 2) * _spacing, _steps)
            .Sweep.ShouldBe((float)-_spacing, 1e-5f);
}
