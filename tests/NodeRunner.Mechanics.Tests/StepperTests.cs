using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class StepperTests
{
    private const double _step = 1.0 / 60;
    private const double _inertia = 2000;
    private static readonly StepperDef _stepper = new(id: 1, nodeId: 1, strength: 500000);
    private static readonly double _lockTolerance = Stepper.LockDistance * 2 * Math.PI / 8;
    private static readonly double _spacing = 2 * Math.PI / _stepper.StepsPerTurn;

    // A frequency so low that a test sees only the release it starts with.
    private static double Slow => 0.1 / _stepper.MaxFrequency;

    // Its output is its frequency at once, as a share of Max frequency (2 Hz), clamped, its sign the way.
    [Fact]
    public void ItsFrequency_IsItsOutput_ReleasingThatOftenTheWayItsSignSays()
    {
        foreach (var (output, frequency, releasesIn10s) in new[] { (1.0, 2.0, 20), (0.5, 1, 10), (-0.5, -1, 10), (-3, -2, 20), (0, 0, 0) })
        {
            var joint = AboutToRelease();
            joint.Run(_step, output, load: 0);

            joint.State.Phase.ShouldBe((0.999 + (Math.Abs(frequency) * _step)) % 1, 1e-12);
            joint.State.Releasing.ShouldBe(frequency != 0);
            joint.State.Direction.ShouldBe(frequency == 0 ? 1 : Math.Sign(frequency));
            var running = new Joint();
            running.Run(10, output, load: 0);
            running.Releases.ShouldBeInRange(Math.Max(0, releasesIn10s - 1), releasesIn10s, $"output {output}");
        }
    }

    // #1263: a sawtooth tick, rising from 0 as it steps to 1 just before the next; its clock never starts over,
    // whatever the rate or way, and stands still, with no step, at frequency 0.
    [Fact]
    public void ItsClock_KeepsTime_AndItsTickRisesToTheNextStep()
    {
        // Without a network it locks its built notch at once with no torque, leaving the hold to Godot's weld.
        var joint = new Joint();
        joint.Run(_step, 0, load: 0);
        (joint.State.Locked, joint.State.Notch, joint.State.Torque, joint.State.Phase).ShouldBe((true, 0, 0, 0));
        Stepper.TickInput(joint.State).ShouldBe(0);
        joint.Run(0.25, 0.5, load: 0);
        Stepper.TickInput(joint.State).ShouldBe(0.25, 1e-9);
        var phase = joint.State.Phase;
        joint.Run(5, 0, load: 0);
        (joint.State.Phase, joint.Releases, joint.State.Notch, joint.State.Locked, joint.State.Torque).ShouldBe((phase, 0, 0, true, 0));
        joint.Run(0.25, 1, load: 0);
        joint.State.Phase.ShouldBe(0.75, 1e-9);
        joint.Run(0.1, -0.5, load: 0);
        joint.State.Phase.ShouldBe(0.85, 1e-9);
        joint.Releases.ShouldBe(0);
        joint.RunUntil(() => joint.Releases == 1, -0.5, load: 0);
        Stepper.TickInput(joint.State).ShouldBe(0, 0.02);
        joint.State.Direction.ShouldBe(-1);

        // Full strength locks a step early; a tenth of it against the load times out and is pulled on.
        foreach (var strength in new[] { 1.0, 0.1 })
        {
            var stepping = new Joint(AboutToRelease().State, Weaker(strength));
            stepping.Run(1.5, Slow, load: -0.08 * _stepper.Strength);
            (stepping.Releases, stepping.State.Locked).ShouldBe((1, true));
            stepping.State.Phase.ShouldBe(0.999 + (1.5 * 0.1) - 1, 1e-9);
        }

        // At 4 Hz a weak push cannot finish a step before the next is due.
        var hurried = new Joint(new StepperState(0.999, 0, false, 1, 0, null), Weaker(0.07, maxFrequency: 4));
        hurried.RunUntil(() => hurried.Releases == 3, 1, load: 0);
        hurried.State.Phase.ShouldBe((0.999 + (hurried.Ticks * 4 * _step)) % 1, 1e-9);
    }

    // Full torque, then braking just in time: the step stops at the notch like a clock tick, not swinging past it.
    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(-1, 1, 0)]
    [InlineData(1, 0.3, 0)]
    [InlineData(1, 1, 0.3)]
    [InlineData(1, 1, -0.3)]
    public void AStep_LandsOnItsNotch_WithoutOvershooting(int way, double strength, double loadShare)
    {
        var joint = new Joint(AboutToRelease().State, Weaker(strength));
        var furthest = 0.0;
        var load = way * loadShare * _stepper.Strength;

        joint.RunUntil(() => (furthest = Math.Max(furthest, way * joint.Angle)) >= 0 && joint.Releases == 1 && !joint.State.Releasing, way * Slow, load);
        var stepTicks = joint.Ticks;
        joint.RunUntil(() => (furthest = Math.Max(furthest, way * joint.Angle)) >= 0 && joint.Ticks >= stepTicks + 60, way * Slow, load);

        furthest.ShouldBeLessThanOrEqualTo(_spacing * (1 + Stepper.LockDistance));
        (joint.State.Notch, joint.State.Locked, joint.Speed).ShouldBe((way, true, 0));
        joint.Angle.ShouldBe(way * _spacing, _lockTolerance);
        if (strength == 1 && loadShare == 0)
        {
            stepTicks.ShouldBeLessThan(30);
        }
    }

    // A foot on the ground makes the joint far heavier than the Stepper reads, so it brakes too late, but it locks the
    // joint where it passes the notch, so the overshoot is still one step. Unwelded, it holds as endless friction, which
    // rings a while on a joint this heavy.
    [Theory]
    [InlineData(1, 1, 4, 0, true)]
    [InlineData(-1, 1, 4, 0, true)]
    [InlineData(1, 1, 16, 0, true)]
    [InlineData(1, 1, 8, 0.3, true)]
    [InlineData(1, 0.5, 16, 0.6, true)]
    [InlineData(1, 1, 16, 0, false)]
    [InlineData(1, 1, 8, 0.3, false)]
    public void OnAJointHeavierThanItReads_AStepStillCountsOnce(int way, double strength, double heavier, double loadShare, bool welded)
    {
        var joint = new Joint(new StepperState(0.99, 0, false, 1, 0, null), Weaker(strength), realInertia: heavier * _inertia, welded: welded);

        joint.Run(welded ? 1.5 : 8, way * Slow, load: way * loadShare * _stepper.Strength);

        (joint.Releases, joint.State.Notch, joint.State.Locked).ShouldBe((1, way, true));
        (way * joint.Angle).ShouldBeInRange(_spacing * (1 - Stepper.LockDistance), 1.5 * _spacing);
        joint.Speed.ShouldBe(0, 1e-6);
    }

    // Once a release starts, the step lands its way whatever the frequency does next.
    [Fact]
    public void AStepUnderWay_Lands_ThoughTheFrequencyStopsOrTurnsRound()
    {
        foreach (var (way, then) in new[] { (1, 0.0), (-1, 0), (1, -1) })
        {
            var joint = AboutToRelease();
            joint.Run(_step, way * Slow, load: 0);
            joint.Run(1.5, then * Slow, load: 0);

            (joint.State.Notch, joint.State.Locked).ShouldBe((way, true), $"way {way}, then {then}");
            joint.Angle.ShouldBe(way * _spacing, _lockTolerance);
        }
    }

    // A friction torque lags a step behind its load, so a sudden one turns it a little before it holds.
    [Theory]
    [InlineData(0.5, 1)]
    [InlineData(-20, 1)]
    [InlineData(0.5, 16)]
    [InlineData(-20, 16)]
    public void WhereItCannotBeWelded_ItHoldsItsNotch_AsEndlessFriction(double share, double heavier)
    {
        var joint = new Joint(welded: false, realInertia: heavier * _inertia);

        joint.Run(5, 0, load: share * _stepper.Strength);
        var held = joint.Angle;
        joint.Run(5, 0, load: share * _stepper.Strength);

        (joint.State.Notch, joint.State.Locked).ShouldBe((0, true));
        joint.Angle.ShouldBe(held, 1e-3);
        joint.Speed.ShouldBe(0, 1e-6);
        joint.State.Torque.ShouldBe(-share * _stepper.Strength, 1);
    }

    [Fact]
    public void AJointOffItsNotch_IsPulledToTheNearest_EvenWhenTheLoadWins()
    {
        foreach (var (at, nearest) in new[] { (0.4, 0), (0.6, 1), (-1.7, -2) })
        {
            var joint = new Joint(new StepperState(0, 0, false, 1, 0, null), angle: at * _spacing);
            joint.Run(2, 0, load: 0.1 * _stepper.Strength);
            (joint.State.Notch, joint.State.Locked).ShouldBe((nearest, true), $"at {at}");
            joint.Angle.ShouldBe(nearest * _spacing, _lockTolerance);
        }

        // Too weak to reach the notch, it counts no step; a load stronger than the push carries it back one,
        // and it locks where it passes it.
        var weak = new Joint(AboutToRelease().State, Weaker(0.05));
        weak.Run(2, Slow, load: -0.04 * _stepper.Strength);
        weak.State.Notch.ShouldBe(0);
        weak.Angle.ShouldBe(0, _lockTolerance);
        var beaten = new Joint(AboutToRelease().State, Weaker(0.1));
        beaten.Run(2, Slow, load: -0.2 * _stepper.Strength);
        (beaten.State.Notch, beaten.State.Locked).ShouldBe((-1, true));
        beaten.Angle.ShouldBeInRange(-1.1 * _spacing, -_spacing + _lockTolerance);
    }

    // A release lasts at most half a second. Cut short by the next, or timed out past halfway, it catches the
    // nearest notch, never back, and the next steps on from it.
    [Fact]
    public void AReleaseCutShort_CatchesTheNearestNotch()
    {
        var feeble = new Joint(AboutToRelease().State, Weaker(0.01));
        var pushingTicks = 0;
        feeble.RunUntil(() => (pushingTicks += feeble.State.Releasing ? 1 : 0) >= 0 && feeble.Ticks > 60, Slow, load: -0.02 * _stepper.Strength);
        pushingTicks.ShouldBe((int)Math.Round(Stepper.MaxReleaseTime / _step));

        var hurried = new Joint(new StepperState(0.999, 0, false, 1, 0, null), Weaker(0.07, maxFrequency: 4));
        hurried.RunUntil(() => hurried.Releases == 2, 1, load: 0);
        (hurried.NotchAtLastRelease, hurried.State.Direction).ShouldBe((1, 1));
        hurried.Angle.ShouldBeInRange(0.5 * _spacing, _spacing * (1 - Stepper.LockDistance));

        var load = -0.08 * _stepper.Strength;
        var slow = new Joint(AboutToRelease().State, Weaker(0.1));
        slow.RunUntil(() => slow.Releases == 1, Slow, load);
        slow.RunUntil(() => !slow.State.Releasing, Slow, load);
        var timedOutAt = slow.Angle;
        slow.State.Notch.ShouldBe(1);
        var lowest = timedOutAt;
        slow.RunUntil(() => (lowest = Math.Min(lowest, slow.Angle)) > double.MinValue && slow.Ticks > 120, Slow, load);
        timedOutAt.ShouldBeInRange(0.5 * _spacing, _spacing * (1 - Stepper.LockDistance));
        lowest.ShouldBeGreaterThan(0.5 * _spacing);
        (slow.State.Notch, slow.State.Locked).ShouldBe((1, true));
    }

    // 5.45 s at 2 Hz is 10 steps of 45°: on across a whole turn, its angle inputs back at 90°, the same every run.
    [Fact]
    public void ItSteps_OnAcrossWholeTurns_CountingNotchesFromTheBuiltAngle()
    {
        var joint = new Joint();
        var again = new Joint();
        joint.Run(5.45, 1, load: 0);
        again.Run(5.45, 1, load: 0);
        (again.State, again.Angle).ShouldBe((joint.State, joint.Angle));

        joint.State.Notch.ShouldBe(10);
        joint.Angle.ShouldBe(10 * _spacing, 0.05);
        var (sin, cos) = Stepper.AngleInputs(joint.Angle);
        sin.ShouldBe(1, 0.05);
        cos.ShouldBe(0, 0.05);
        Stepper.NotchAngle(-3, _stepper).ShouldBe(-3 * Math.PI / 4, 1e-12);
    }

    private static Joint AboutToRelease() => new(new StepperState(0.999, 0, false, 1, 0, null));

    // A Stepper with a share of the test Stepper's strength: a weaker push against the same loads.
    private static StepperDef Weaker(double share, double maxFrequency = StepperDef.DefaultMaxFrequency) =>
        new(id: 1, nodeId: 1, strength: share * _stepper.Strength, maxFrequency: maxFrequency);

    /// <summary>
    /// A turning joint with a fixed inertia, stepped as Godot steps a body. The Stepper reads its inertia as
    /// <c>_inertia</c>; <c>realInertia</c> is what it really turns. Unless <c>welded</c> is false it is rigid while the
    /// Stepper is locked, as Godot's weld makes it.
    /// </summary>
    private sealed class Joint(StepperState state = default, StepperDef? stepper = null, double realInertia = _inertia, double angle = 0, bool welded = true)
    {
        private readonly StepperDef _def = stepper ?? _stepper;

        public StepperState State { get; private set; } = state;

        public double Angle { get; private set; } = angle;

        public double Speed { get; private set; }

        public int Releases { get; private set; }

        /// <summary>The notch it held as its last release began, which that release steps on from.</summary>
        public int NotchAtLastRelease { get; private set; }

        public int Ticks { get; private set; }

        public void Run(double seconds, double frequencyOutput, double load)
        {
            for (var tick = 0; tick < (int)Math.Round(seconds / _step); tick++)
            {
                Tick(frequencyOutput, load);
            }
        }

        public void RunUntil(Func<bool> done, double frequencyOutput, double load)
        {
            while (!done())
            {
                Ticks.ShouldBeLessThan(6000, "the joint never got there");
                Tick(frequencyOutput, load);
            }
        }

        private void Tick(double frequencyOutput, double load)
        {
            State = Stepper.Next(_def, Angle, Speed, frequencyOutput, State, _step, _inertia, welded);
            if (State.Releasing && State.ReleaseTime == 0)
            {
                Releases++;
                NotchAtLastRelease = State.Notch;
            }

            Speed = State.Locked && welded ? 0 : Speed + ((State.Torque + load) / realInertia * _step);
            Angle += Speed * _step;
            Ticks++;
        }
    }
}
