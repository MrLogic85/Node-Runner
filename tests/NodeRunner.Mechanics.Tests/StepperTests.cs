using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class StepperTests
{
    private const double _step = 1.0 / 60;
    private const double _inertia = 2000;
    private static readonly StepperDef _stepper = new(id: 1, nodeId: 1);
    private static readonly double _spacing = 2 * Math.PI / _stepper.StepsPerTurn;

    [Fact]
    public void TheRunningFrequency_FollowsItsOutput_WithATimeConstantOfOneSecond()
    {
        var joint = new Joint();

        joint.Run(seconds: 1, frequencyOutput: 1, powerOutput: 0, load: 0);

        joint.State.Frequency.ShouldBe(_stepper.MaxFrequency * (1 - Math.Exp(-1)), tolerance: 1e-9);
        Stepper.FrequencyInput(joint.State, _stepper).ShouldBe(1 - Math.Exp(-1), tolerance: 1e-9);
    }

    [Theory]
    [InlineData(1, 20)]
    [InlineData(0.5, 10)]
    [InlineData(0, 0)]
    public void ItReleases_AtItsRunningFrequency(double frequencyOutput, int releases)
    {
        var joint = new Joint(new StepperState(frequencyOutput * _stepper.MaxFrequency, 0, 0, false, 1, 0, null));

        joint.Run(seconds: 10, frequencyOutput, powerOutput: 0.5, load: 0);

        joint.Releases.ShouldBeInRange(Math.Max(0, releases - 1), releases);
    }

    [Theory]
    [InlineData(0.5, 1)]
    [InlineData(-0.5, -1)]
    [InlineData(1, 1)]
    [InlineData(-1, -1)]
    public void EachRelease_StepsOneNotch_InPowersDirection(double power, int notch)
    {
        var joint = JointAboutToRelease();

        joint.Run(seconds: 1.5, frequencyOutput: SlowFrequency, power, load: 0);

        joint.Releases.ShouldBe(1);
        joint.State.Notch.ShouldBe(notch);
        joint.State.Releasing.ShouldBeFalse();
        joint.Angle.ShouldBe(notch * _spacing, tolerance: 1e-6);
        joint.Speed.ShouldBe(0, tolerance: 1e-4);
    }

    [Fact]
    public void PowerTurningRoundMidRelease_KeepsTheStepItsWay()
    {
        var joint = JointAboutToRelease();
        joint.Run(seconds: 1.0 / 60, frequencyOutput: SlowFrequency, powerOutput: 0.5, load: 0);

        joint.Run(seconds: 1.5, frequencyOutput: SlowFrequency, powerOutput: -0.5, load: 0);

        joint.State.Notch.ShouldBe(1);
        joint.Angle.ShouldBe(_spacing, tolerance: 1e-6);
    }

    [Fact]
    public void ItSteps_OnAcrossWholeTurns()
    {
        var joint = new Joint(new StepperState(_stepper.MaxFrequency, 0, 0, false, 1, 0, null));

        joint.Run(seconds: 5.45, frequencyOutput: 1, powerOutput: 0.5, load: 0);

        joint.State.Notch.ShouldBe(10);
        joint.Angle.ShouldBe(10 * _spacing, tolerance: 0.05);
        var (sin, cos) = Stepper.AngleInputs(joint.Angle);
        sin.ShouldBe(Math.Sin(2 * _spacing), tolerance: 0.05);
        cos.ShouldBe(Math.Cos(2 * _spacing), tolerance: 0.05);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(-0.9)]
    public void WithoutABrain_ItHoldsItsNotch_AgainstALoadWithinHoldStrength(double share)
    {
        var joint = new Joint();

        joint.Run(seconds: 5, frequencyOutput: 0, powerOutput: 0, load: share * _stepper.HoldStrength);

        joint.State.Notch.ShouldBe(0);
        joint.Angle.ShouldBe(0, tolerance: 1e-6);
        joint.State.Torque.ShouldBe(-share * _stepper.HoldStrength, tolerance: 1);
    }

    [Theory]
    [InlineData(0.99)]
    [InlineData(-0.99)]
    public void ALoadGrowingToJustWithinHoldStrength_IsHeldAtItsNotch(double share)
    {
        var joint = new Joint();
        for (var tick = 1; tick <= 120; tick++)
        {
            joint.Run(seconds: _step, frequencyOutput: 0, powerOutput: 0, load: share * _stepper.HoldStrength * tick / 120);
        }

        joint.Run(seconds: 10, frequencyOutput: 0, powerOutput: 0, load: share * _stepper.HoldStrength);

        joint.State.Notch.ShouldBe(0);
        joint.Angle.ShouldBe(0, tolerance: 1e-6);
        joint.Speed.ShouldBe(0, tolerance: 1e-6);
    }

    [Theory]
    [InlineData(0.92)]
    [InlineData(0.95)]
    [InlineData(0.99)]
    [InlineData(-0.99)]
    public void ASuddenLoadJustWithinHoldStrength_MaySlipAFewNotches_ButComesToRestAtOne(double share)
    {
        // The hold feels a load a step late, as a Brake does, so a sudden one near its limit carries the
        // joint on until the little strength left over stops it.
        var joint = new Joint();

        joint.Run(seconds: 10, frequencyOutput: 0, powerOutput: 0, load: share * _stepper.HoldStrength);

        joint.Speed.ShouldBe(0, tolerance: 1e-6);
        joint.Angle.ShouldBe(joint.State.Notch * _spacing, tolerance: 1e-6);
        Math.Abs(joint.State.Notch).ShouldBeLessThanOrEqualTo(6);
    }

    [Fact]
    public void ALastingLoadPastHoldStrength_SlipsOnThroughNotches_BrakedByTheFullHold()
    {
        var joint = new Joint();

        joint.Run(seconds: 1, frequencyOutput: 0, powerOutput: 0, load: 1.5 * _stepper.HoldStrength);
        var before = joint.Speed;
        joint.Run(seconds: _step, frequencyOutput: 0, powerOutput: 0, load: 1.5 * _stepper.HoldStrength);

        joint.State.Torque.ShouldBe(-_stepper.HoldStrength);
        (joint.Speed - before).ShouldBe(0.5 * _stepper.HoldStrength * _step / _inertia, tolerance: 1e-9);
        joint.State.Notch.ShouldBeGreaterThan(_stepper.StepsPerTurn);
    }

    [Theory]
    [InlineData(1.54, 2)]
    [InlineData(1.3, 1)]
    public void AnOverloadEasedBetweenNotches_IsCaughtAtTheNotchNearestTheJoint(double easedAt, int caught)
    {
        var joint = new Joint();
        var overload = 1.02 * _stepper.HoldStrength;
        joint.RunUntil(() => joint.Angle >= easedAt * _spacing, frequencyOutput: 0, powerOutput: 0, overload);

        joint.Run(seconds: 3, frequencyOutput: 0, powerOutput: 0, load: 0.5 * _stepper.HoldStrength);

        joint.State.Notch.ShouldBe(caught);
        joint.Angle.ShouldBe(caught * _spacing, tolerance: 1e-6);
        joint.Speed.ShouldBe(0, tolerance: 1e-6);
    }

    [Fact]
    public void AtPowerZero_ItLetsGo_AndCatchesTheNearestNotch()
    {
        var joint = JointAboutToRelease();
        var load = 0.005 * _stepper.HoldStrength;

        joint.Run(seconds: 0.25, frequencyOutput: SlowFrequency, powerOutput: 0, load);
        var drifted = joint.Angle;
        var pushed = joint.State.Torque;
        joint.Run(seconds: 2, frequencyOutput: SlowFrequency, powerOutput: 0, load);

        drifted.ShouldBeGreaterThan(0);
        pushed.ShouldBe(0);
        joint.State.Notch.ShouldBe(0);
        joint.Angle.ShouldBe(0, tolerance: 1e-6);
    }

    [Fact]
    public void APushTooWeakToReachTheNotch_DoesNotCountAStep()
    {
        var joint = JointAboutToRelease();

        joint.Run(seconds: 2, frequencyOutput: SlowFrequency, powerOutput: 0.05, load: -0.04 * _stepper.Strength);

        joint.State.Notch.ShouldBe(0);
        joint.Angle.ShouldBe(0, tolerance: 1e-6);
    }

    [Fact]
    public void ALoadStrongerThanThePush_CarriesItBackOneNotch()
    {
        var joint = JointAboutToRelease();

        joint.Run(seconds: 2, frequencyOutput: SlowFrequency, powerOutput: 0.1, load: -0.2 * _stepper.Strength);

        joint.State.Notch.ShouldBe(-1);
        joint.Angle.ShouldBe(-_spacing, tolerance: 1e-6);
    }

    [Fact]
    public void AReleaseCutShortByTheNext_CatchesTheNearestNotch_AndTheNextStepsOnFromIt()
    {
        // At 4 Hz a release has a quarter second: this push gets the joint past halfway, not all the way.
        var stepper = new StepperDef(id: 1, nodeId: 1, maxFrequency: 4);
        var joint = new Joint(new StepperState(stepper.MaxFrequency, 0.999, 0, false, 1, 0, null), stepper);

        joint.RunUntil(() => joint.Releases == 2, frequencyOutput: 1, powerOutput: 0.07, load: 0);

        joint.NotchAtLastRelease.ShouldBe(1);
        joint.Angle.ShouldBeInRange(0.5 * _spacing, _spacing);
    }

    [Fact]
    public void ARelease_LastsNoMoreThanHalfASecond()
    {
        var joint = JointAboutToRelease();
        var pushingTicks = 0;

        joint.RunUntil(
            () =>
            {
                pushingTicks += joint.State.Releasing ? 1 : 0;
                return joint.Ticks > 60;
            },
            frequencyOutput: SlowFrequency,
            powerOutput: 0.01,
            load: -0.02 * _stepper.Strength);

        pushingTicks.ShouldBe((int)Math.Round(Stepper.MaxReleaseTime / _step));
    }

    [Fact]
    public void ItIsDeterministic()
    {
        var first = new Joint();
        var second = new Joint();

        first.Run(seconds: 3, frequencyOutput: 0.7, powerOutput: -0.6, load: 0.1 * _stepper.HoldStrength);
        second.Run(seconds: 3, frequencyOutput: 0.7, powerOutput: -0.6, load: 0.1 * _stepper.HoldStrength);

        second.State.ShouldBe(first.State);
        second.Angle.ShouldBe(first.Angle);
    }

    [Fact]
    public void NotchAngle_CountsFromTheBuiltAngle() =>
        Stepper.NotchAngle(-3, _stepper).ShouldBe(-3 * Math.PI / 4, tolerance: 1e-12);

    // A frequency so low that a test sees only the release it starts with.
    private static double SlowFrequency => 0.1 / _stepper.MaxFrequency;

    private static Joint JointAboutToRelease() => new(new StepperState(0.1, 0.999, 0, false, 1, 0, null));

    /// <summary>A turning joint with a fixed inertia, stepped as Godot steps a body.</summary>
    private sealed class Joint(StepperState state = default, StepperDef? stepper = null)
    {
        private readonly StepperDef _def = stepper ?? _stepper;

        public StepperState State { get; private set; } = state;

        public double Angle { get; private set; }

        public double Speed { get; private set; }

        public int Releases { get; private set; }

        /// <summary>The notch it held as its last release began, which that release steps on from.</summary>
        public int NotchAtLastRelease { get; private set; }

        public int Ticks { get; private set; }

        public void Run(double seconds, double frequencyOutput, double powerOutput, double load)
        {
            for (var tick = 0; tick < (int)Math.Round(seconds / _step); tick++)
            {
                Tick(frequencyOutput, powerOutput, load);
            }
        }

        public void RunUntil(Func<bool> done, double frequencyOutput, double powerOutput, double load)
        {
            while (!done())
            {
                Ticks.ShouldBeLessThan(6000, "the joint never got there");
                Tick(frequencyOutput, powerOutput, load);
            }
        }

        private void Tick(double frequencyOutput, double powerOutput, double load)
        {
            State = Stepper.Next(_def, Angle, Speed, frequencyOutput, powerOutput, State, _step, _inertia);
            if (State.Releasing && State.ReleaseTime == 0)
            {
                Releases++;
                NotchAtLastRelease = State.Notch;
            }

            Speed += (State.Torque + load) / _inertia * _step;
            Angle += Speed * _step;
            Ticks++;
        }
    }
}
