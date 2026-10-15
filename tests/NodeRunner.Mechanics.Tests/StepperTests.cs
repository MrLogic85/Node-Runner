using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class StepperTests
{
    private const double _step = 1.0 / 60;
    private const double _inertia = 2000;
    private static readonly StepperDef _stepper = new(id: 1, nodeId: 1, strength: 500000);
    private static readonly double _lockTolerance = Stepper.LockDistance * 2 * Math.PI / 8;
    private static readonly double _spacing = 2 * Math.PI / _stepper.StepsPerTurn;

    [Theory]
    [InlineData(1, 2)]
    [InlineData(0.5, 1)]
    [InlineData(-0.5, -1)]
    [InlineData(-3, -2)]
    [InlineData(0, 0)]
    public void ItsFrequency_IsItsOutputAtOnce_ItsSignTheWay(double frequencyOutput, double frequency)
    {
        var joint = JointAboutToRelease();

        joint.Run(seconds: _step, frequencyOutput, load: 0);

        joint.State.Phase.ShouldBe((0.999 + (Math.Abs(frequency) * _step)) % 1, tolerance: 1e-12);
        joint.State.Releasing.ShouldBe(frequency != 0);
        if (frequency != 0)
        {
            joint.State.Direction.ShouldBe(Math.Sign(frequency));
        }
    }

    [Theory]
    [InlineData(1, 20)]
    [InlineData(0.5, 10)]
    [InlineData(-1, 20)]
    [InlineData(-0.5, 10)]
    [InlineData(0, 0)]
    public void ItReleases_AtItsFrequency_EitherWay(double frequencyOutput, int releases)
    {
        var joint = new Joint();

        joint.Run(seconds: 10, frequencyOutput, load: 0);

        joint.Releases.ShouldBeInRange(Math.Max(0, releases - 1), releases);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(-1, -1)]
    public void EachRelease_StepsOneNotch_TheWayItsFrequencySays(int way, int notch)
    {
        var joint = JointAboutToRelease();

        joint.Run(seconds: 1.5, frequencyOutput: way * SlowFrequency, load: 0);

        joint.Releases.ShouldBe(1);
        joint.State.Notch.ShouldBe(notch);
        joint.State.Releasing.ShouldBeFalse();
        joint.State.Locked.ShouldBeTrue();
        joint.Angle.ShouldBe(notch * _spacing, tolerance: _lockTolerance);
        joint.Speed.ShouldBe(0);
    }

    // #1263: 1 as it steps, 0 halfway to the next step, smooth between.
    [Fact]
    public void ItsTick_IsOneAsItSteps_AndZeroHalfwayToTheNext()
    {
        // Half its Max frequency is one step a second: 60 physics steps.
        var joint = new Joint();
        Stepper.TickInput(joint.State).ShouldBe(1);

        joint.Run(seconds: 0.25, frequencyOutput: 0.5, load: 0);
        Stepper.TickInput(joint.State).ShouldBe(0.25, tolerance: 1e-9);

        joint.Run(seconds: 0.25, frequencyOutput: 0.5, load: 0);
        Stepper.TickInput(joint.State).ShouldBe(0, tolerance: 1e-9);
        joint.Releases.ShouldBe(0);

        joint.RunUntil(() => joint.Releases == 1, frequencyOutput: 0.5, load: 0);
        joint.Ticks.ShouldBeInRange(59, 61);
        Stepper.TickInput(joint.State).ShouldBe(1, tolerance: 0.07);
    }

    [Fact]
    public void ItsClock_KeepsTimeAcrossChangesInRate_WithoutStartingOver()
    {
        var joint = new Joint();

        joint.Run(seconds: 0.25, frequencyOutput: 0.5, load: 0);
        joint.Run(seconds: 0.25, frequencyOutput: 1, load: 0);

        joint.State.Phase.ShouldBe(0.75, tolerance: 1e-9);
        joint.Releases.ShouldBe(0);
    }

    [Fact]
    public void ItsClock_KeepsTimeAsItsFrequencyTurnsRound()
    {
        var joint = new Joint();

        joint.Run(seconds: 0.25, frequencyOutput: 0.5, load: 0);
        joint.Run(seconds: 0.25, frequencyOutput: -0.5, load: 0);

        joint.State.Phase.ShouldBe(0.5, tolerance: 1e-9);
    }

    [Fact]
    public void AtFrequencyZero_ItsClockAndTickStandStill_AndItDoesNotStep()
    {
        var joint = new Joint();
        joint.Run(seconds: 0.25, frequencyOutput: 0.5, load: 0);
        var phase = joint.State.Phase;

        joint.Run(seconds: 5, frequencyOutput: 0, load: 0);

        joint.State.Phase.ShouldBe(phase);
        Stepper.TickInput(joint.State).ShouldBe(0.25, tolerance: 1e-9);
        joint.Releases.ShouldBe(0);
        joint.State.Notch.ShouldBe(0);
        joint.State.Locked.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void AtFrequencyZeroMidRelease_TheStepStillLands(int way)
    {
        var joint = JointAboutToRelease();
        joint.Run(seconds: _step, frequencyOutput: way * SlowFrequency, load: 0);

        joint.Run(seconds: 1.5, frequencyOutput: 0, load: 0);

        joint.State.Notch.ShouldBe(way);
        joint.State.Locked.ShouldBeTrue();
        joint.Angle.ShouldBe(way * _spacing, tolerance: _lockTolerance);
    }

    // #1263: the clock keeps time; the lock, a timeout or a step cut short never reset it.
    [Theory]
    [InlineData(1.0)]
    [InlineData(0.1)]
    public void ItsClock_KeepsTimeThroughAStep_HoweverTheStepEnds(double strength)
    {
        // Full strength locks the step early; a tenth of it against the load times out and is pulled on.
        var joint = new Joint(JointAboutToRelease().State, Weaker(strength));

        joint.Run(seconds: 1.5, frequencyOutput: SlowFrequency, load: -0.08 * _stepper.Strength);

        joint.Releases.ShouldBe(1);
        joint.State.Locked.ShouldBeTrue();
        joint.State.Phase.ShouldBe(0.999 + (1.5 * 0.1) - 1, tolerance: 1e-9);
    }

    [Fact]
    public void AReleaseDueBeforeTheLastLocks_LeavesItsClockTicking()
    {
        // At 4 Hz a weak push cannot finish a step before the next is due.
        var stepper = Weaker(0.07, maxFrequency: 4);
        var joint = new Joint(new StepperState(0.999, 0, false, 1, 0, null), stepper);

        joint.RunUntil(() => joint.Releases == 3, frequencyOutput: 1, load: 0);

        joint.State.Phase.ShouldBe((0.999 + (joint.Ticks * 4 * _step)) % 1, tolerance: 1e-9);
    }

    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(-1, 1, 0)]
    [InlineData(1, 0.3, 0)]
    [InlineData(1, 1, 0.3)]
    [InlineData(1, 1, -0.3)]
    public void AStep_LandsOnItsNotchLikeAClockTick_WithoutOvershooting(int way, double strength, double loadShare)
    {
        // Full torque, then braking just in time: the step stops at the notch instead of swinging past it.
        var joint = new Joint(JointAboutToRelease().State, Weaker(strength));
        var furthest = 0.0;

        joint.RunUntil(
            () =>
            {
                furthest = Math.Max(furthest, way * joint.Angle);
                return joint.Releases == 1 && !joint.State.Releasing;
            },
            way * SlowFrequency,
            load: way * loadShare * _stepper.Strength);
        var stepTicks = joint.Ticks;
        for (var tick = 0; tick < 60; tick++)
        {
            joint.Run(seconds: _step, way * SlowFrequency, load: way * loadShare * _stepper.Strength);
            furthest = Math.Max(furthest, way * joint.Angle);
        }

        furthest.ShouldBeLessThanOrEqualTo(_spacing * (1 + Stepper.LockDistance));
        joint.State.Notch.ShouldBe(way);
        joint.State.Locked.ShouldBeTrue();
        joint.Angle.ShouldBe(way * _spacing, tolerance: _lockTolerance);
        if (strength == 1 && loadShare == 0)
        {
            stepTicks.ShouldBeLessThan(30);
        }
    }

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
        // A foot on the ground makes the joint far heavier than the Stepper reads, so it brakes too late,
        // but it locks the joint where it passes the notch, so the overshoot is still one step.
        var joint = new Joint(new StepperState(0.99, 0, false, 1, 0, null), Weaker(strength), realInertia: heavier * _inertia, welded: welded);

        // Without a weld it holds as endless friction, which rings a while on a joint this heavy.
        joint.Run(seconds: welded ? 1.5 : 8, way * SlowFrequency, load: way * loadShare * _stepper.Strength);

        joint.Releases.ShouldBe(1);
        joint.State.Notch.ShouldBe(way);
        joint.State.Locked.ShouldBeTrue();
        (way * joint.Angle).ShouldBeInRange(_spacing * (1 - Stepper.LockDistance), 1.5 * _spacing);
        joint.Speed.ShouldBe(0, tolerance: 1e-6);
    }

    [Fact]
    public void AFrequencyTurningRoundMidRelease_KeepsTheStepItsWay()
    {
        var joint = JointAboutToRelease();
        joint.Run(seconds: _step, frequencyOutput: SlowFrequency, load: 0);

        joint.Run(seconds: 1.5, frequencyOutput: -SlowFrequency, load: 0);

        joint.State.Notch.ShouldBe(1);
        joint.Angle.ShouldBe(_spacing, tolerance: _lockTolerance);
    }

    [Fact]
    public void ItSteps_OnAcrossWholeTurns()
    {
        var joint = new Joint();

        joint.Run(seconds: 5.45, frequencyOutput: 1, load: 0);

        joint.State.Notch.ShouldBe(10);
        joint.Angle.ShouldBe(10 * _spacing, tolerance: 0.05);
        var (sin, cos) = Stepper.AngleInputs(joint.Angle);
        sin.ShouldBe(Math.Sin(2 * _spacing), tolerance: 0.05);
        cos.ShouldBe(Math.Cos(2 * _spacing), tolerance: 0.05);
    }

    [Fact]
    public void ItLocksItsBuiltNotch_OnItsFirstStep_AndGivesNoTorqueThere()
    {
        var joint = new Joint();

        joint.Run(seconds: _step, frequencyOutput: 0, load: 0);

        joint.State.Locked.ShouldBeTrue();
        joint.State.Notch.ShouldBe(0);
        joint.State.Torque.ShouldBe(0);
    }

    [Fact]
    public void WithoutABrain_ItStaysLockedAtItsNotch_AndLeavesTheHoldToTheWeld()
    {
        // Against a load the weld holds: Godot's, so the phone test plan checks it.
        var joint = new Joint();

        joint.Run(seconds: 5, frequencyOutput: 0, load: 0);

        joint.State.Notch.ShouldBe(0);
        joint.State.Locked.ShouldBeTrue();
        joint.State.Torque.ShouldBe(0);
    }

    [Theory]
    [InlineData(0.5, 1)]
    [InlineData(-20, 1)]
    [InlineData(0.5, 16)]
    [InlineData(-20, 16)]
    public void WhereItCannotBeWelded_ItHoldsItsNotch_AsEndlessFriction(double share, double heavier)
    {
        var joint = new Joint(welded: false, realInertia: heavier * _inertia);

        // A friction torque lags a step behind its load, so a sudden one turns it a little before it holds.
        joint.Run(seconds: 5, frequencyOutput: 0, load: share * _stepper.Strength);
        var held = joint.Angle;
        joint.Run(seconds: 5, frequencyOutput: 0, load: share * _stepper.Strength);

        joint.State.Notch.ShouldBe(0);
        joint.State.Locked.ShouldBeTrue();
        joint.Angle.ShouldBe(held, tolerance: 1e-3);
        joint.Speed.ShouldBe(0, tolerance: 1e-6);
        joint.State.Torque.ShouldBe(-share * _stepper.Strength, tolerance: 1);
    }

    [Theory]
    [InlineData(0.4, 0)]
    [InlineData(0.6, 1)]
    [InlineData(-1.7, -2)]
    public void AJointLeftBetweenNotches_IsPulledToTheNearest_AndLockedThere(double at, int nearest)
    {
        var joint = new Joint(new StepperState(0, 0, false, 1, 0, null), angle: at * _spacing);

        joint.Run(seconds: 2, frequencyOutput: 0, load: 0.1 * _stepper.Strength);

        joint.State.Notch.ShouldBe(nearest);
        joint.State.Locked.ShouldBeTrue();
        joint.Angle.ShouldBe(nearest * _spacing, tolerance: _lockTolerance);
    }

    [Fact]
    public void APushTooWeakToReachTheNotch_DoesNotCountAStep()
    {
        var joint = new Joint(JointAboutToRelease().State, Weaker(0.05));

        joint.Run(seconds: 2, frequencyOutput: SlowFrequency, load: -0.04 * _stepper.Strength);

        joint.State.Notch.ShouldBe(0);
        joint.Angle.ShouldBe(0, tolerance: _lockTolerance);
    }

    [Fact]
    public void ALoadStrongerThanThePush_CarriesItBackOneNotch_AndItLocksWhereItPassesIt()
    {
        var joint = new Joint(JointAboutToRelease().State, Weaker(0.1));

        joint.Run(seconds: 2, frequencyOutput: SlowFrequency, load: -0.2 * _stepper.Strength);

        joint.State.Notch.ShouldBe(-1);
        joint.State.Locked.ShouldBeTrue();
        joint.Angle.ShouldBeInRange(-1.1 * _spacing, -_spacing + _lockTolerance);
    }

    [Fact]
    public void AReleaseCutShortByTheNext_CatchesTheNearestNotch_AndTheNextStepsOnFromIt()
    {
        // At 4 Hz a release has a quarter second: this push gets the joint past halfway, not all the way.
        var stepper = Weaker(0.07, maxFrequency: 4);
        var joint = new Joint(new StepperState(0.999, 0, false, 1, 0, null), stepper);

        joint.RunUntil(() => joint.Releases == 2, frequencyOutput: 1, load: 0);

        joint.NotchAtLastRelease.ShouldBe(1);
        joint.State.Direction.ShouldBe(1);
        joint.Angle.ShouldBeInRange(0.5 * _spacing, _spacing * (1 - Stepper.LockDistance));
    }

    [Fact]
    public void AReleaseTimingOutPastHalfway_PullsOnToTheNearestNotch_NeverBack()
    {
        var joint = new Joint(JointAboutToRelease().State, Weaker(0.1));
        var load = -0.08 * _stepper.Strength;
        joint.RunUntil(() => joint.Releases == 1, SlowFrequency, load);

        joint.RunUntil(() => !joint.State.Releasing, SlowFrequency, load);
        var timedOutAt = joint.Angle;
        var notch = joint.State.Notch;
        var lowest = timedOutAt;
        joint.RunUntil(
            () =>
            {
                lowest = Math.Min(lowest, joint.Angle);
                return joint.Ticks > 120;
            },
            SlowFrequency,
            load);

        timedOutAt.ShouldBeInRange(0.5 * _spacing, _spacing * (1 - Stepper.LockDistance));
        notch.ShouldBe(1);
        lowest.ShouldBeGreaterThan(0.5 * _spacing);
        joint.State.Notch.ShouldBe(1);
        joint.State.Locked.ShouldBeTrue();
    }

    [Fact]
    public void ARelease_LastsNoMoreThanHalfASecond()
    {
        var joint = new Joint(JointAboutToRelease().State, Weaker(0.01));
        var pushingTicks = 0;

        joint.RunUntil(
            () =>
            {
                pushingTicks += joint.State.Releasing ? 1 : 0;
                return joint.Ticks > 60;
            },
            frequencyOutput: SlowFrequency,
            load: -0.02 * _stepper.Strength);

        pushingTicks.ShouldBe((int)Math.Round(Stepper.MaxReleaseTime / _step));
    }

    [Fact]
    public void ItIsDeterministic()
    {
        var first = new Joint();
        var second = new Joint();

        first.Run(seconds: 3, frequencyOutput: -0.7, load: 0.1 * _stepper.Strength);
        second.Run(seconds: 3, frequencyOutput: -0.7, load: 0.1 * _stepper.Strength);

        second.State.ShouldBe(first.State);
        second.Angle.ShouldBe(first.Angle);
    }

    [Fact]
    public void NotchAngle_CountsFromTheBuiltAngle() =>
        Stepper.NotchAngle(-3, _stepper).ShouldBe(-3 * Math.PI / 4, tolerance: 1e-12);

    // A frequency so low that a test sees only the release it starts with.
    private static double SlowFrequency => 0.1 / _stepper.MaxFrequency;

    private static Joint JointAboutToRelease() => new(new StepperState(0.999, 0, false, 1, 0, null));

    // A Stepper with a share of the test Stepper's strength: a weaker push against the same loads.
    private static StepperDef Weaker(double share, double maxFrequency = StepperDef.DefaultMaxFrequency) =>
        new(id: 1, nodeId: 1, strength: share * _stepper.Strength, maxFrequency: maxFrequency);

    /// <summary>
    /// A turning joint with a fixed inertia, stepped as Godot steps a body. The Stepper reads its inertia
    /// as <c>_inertia</c>; <c>realInertia</c> is what it really turns. Unless <c>welded</c> is false it
    /// is rigid while the Stepper is locked, as Godot's weld makes it.
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
