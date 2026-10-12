namespace NodeRunner.Mechanics.Tests;

public sealed class BrakeTests
{
    private const double _step = 1.0 / 60;
    private const double _inertia = 2000;
    private const double _strength = 2_000_000;

    [Fact]
    public void Next_NeverGivesMoreThanItsStrength()
    {
        Brake.Next(speed: 100, _strength, null, _step, _inertia).Torque.ShouldBe(-_strength);
        Brake.Next(speed: -100, _strength, null, _step, _inertia).Torque.ShouldBe(_strength);
    }

    [Fact]
    public void Next_WithInfiniteStrength_IsNeverCapped()
    {
        Brake.Next(speed: 1e6, double.PositiveInfinity, null, _step, _inertia)
            .Torque.ShouldBe(-1e6 * _inertia / _step);
    }

    [Fact]
    public void Next_AtRest_GivesNothing() =>
        Brake.Next(speed: 0, _strength, null, _step, _inertia).Torque.ShouldBe(0);

    [Theory]
    [InlineData(3)]
    [InlineData(-3)]
    public void AFreeJoint_StopsWithoutTurningBack(double speed)
    {
        var joint = new Joint(speed);

        joint.Run(steps: 120, load: 0);

        joint.Speed.ShouldBe(0, tolerance: 1e-9);
        joint.Reversed.ShouldBeFalse();
    }

    [Fact]
    public void AWeakBrake_SlowsAFreeJoint_AtItsStrength()
    {
        var joint = new Joint(speed: 30);
        var before = joint.Speed;

        joint.Step(load: 0);

        joint.Speed.ShouldBe(before - (_strength * _step / _inertia), tolerance: 1e-9);
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(0.5)]
    [InlineData(0.9)]
    [InlineData(-0.9)]
    public void ASuddenLoadWithinStrength_IsHeld(double share)
    {
        var joint = new Joint(speed: 0);

        joint.Run(steps: 10, load: share * _strength);
        var settled = joint.Angle;
        joint.Run(steps: 600, load: share * _strength);

        (joint.Angle - settled).ShouldBe(0, tolerance: 1e-9);
        joint.Brake!.Value.Torque.ShouldBe(-share * _strength, tolerance: 1e-3);
    }

    [Fact]
    public void ALoadPastStrength_Slips_BrakedByTheFullStrength()
    {
        var joint = new Joint(speed: 0);

        joint.Run(steps: 60, load: 1.5 * _strength);
        var before = joint.Speed;
        joint.Step(load: 1.5 * _strength);

        joint.Brake!.Value.Torque.ShouldBe(-_strength);
        (joint.Speed - before).ShouldBe(0.5 * _strength * _step / _inertia, tolerance: 1e-9);
    }

    [Fact]
    public void AfterSlipping_ItGripsAgain_WhenTheLoadEases()
    {
        var joint = new Joint(speed: 0);
        joint.Run(steps: 10, load: 1.5 * _strength);

        joint.Run(steps: 30, load: 0.5 * _strength);
        var settled = joint.Angle;
        joint.Run(steps: 120, load: 0.5 * _strength);

        joint.Speed.ShouldBe(0, tolerance: 1e-9);
        (joint.Angle - settled).ShouldBe(0, tolerance: 1e-9);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0)]
    public void AfterSlipping_ItStopsWithoutTurningBack_WhenTheLoadEases(double easedShare)
    {
        var joint = new Joint(speed: 0);
        joint.Run(steps: 10, load: 1.5 * _strength);

        joint.Run(steps: 120, load: easedShare * _strength);

        joint.Reversed.ShouldBeFalse();
        joint.Speed.ShouldBe(0, tolerance: 1e-9);
    }

    [Theory]
    [InlineData(0.3)]
    [InlineData(1.3)]
    public void AnInertiaUpToAThirdTooHeavy_StillHolds(double estimateShare)
    {
        var joint = new Joint(speed: 0, estimate: estimateShare * _inertia);

        joint.Run(steps: 300, load: 0.5 * _strength);
        var settled = joint.Angle;
        joint.Run(steps: 300, load: 0.5 * _strength);

        (joint.Angle - settled).ShouldBe(0, tolerance: 1e-6);
        joint.Brake!.Value.Torque.ShouldBe(-0.5 * _strength, tolerance: 1);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(0.1)]
    [InlineData(1.3)]
    public void TakesEnergyOverARun_EvenWithAWrongInertia(double estimateShare)
    {
        var joint = new Joint(speed: 5, estimate: estimateShare * _inertia);

        joint.Run(steps: 120, load: 0.5 * _strength);
        joint.Run(steps: 120, load: -0.8 * _strength);
        joint.Run(steps: 600, load: 0);

        joint.BrakeWork.ShouldBeLessThanOrEqualTo(1e-6);
        joint.Speed.ShouldBe(0, tolerance: 1e-6);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void Next_WithoutAPositiveStrength_Throws(double strength) =>
        Should.Throw<ArgumentOutOfRangeException>(() => Brake.Next(0, strength, null, _step, _inertia));

    [Theory]
    [InlineData(0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Next_WithoutAPositiveFiniteStepOrInertia_Throws(double value)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Brake.Next(0, _strength, null, value, _inertia));
        Should.Throw<ArgumentOutOfRangeException>(() => Brake.Next(0, _strength, null, _step, value));
    }

    // A joint of one known inertia under a steady outside torque, stepped as Godot steps a body:
    // the brake reads the speed, then its torque and the load act together for one step.
    private sealed class Joint(double speed, double estimate = _inertia)
    {
        public double Speed { get; private set; } = speed;

        public double Angle { get; private set; }

        public BrakeState? Brake { get; private set; }

        public double BrakeWork { get; private set; }

        // The way it first moved; it turned back once it moves the other way.
        private double _direction;

        public bool Reversed { get; private set; }

        public void Run(int steps, double load)
        {
            for (var i = 0; i < steps; i++)
            {
                Step(load);
            }
        }

        public void Step(double load)
        {
            Brake = Mechanics.Brake.Next(Speed, _strength, Brake, _step, estimate);
            var before = Speed;
            var torque = Brake.Value.Torque;
            Speed += (torque + load) * _step / _inertia;
            Angle += Speed * _step;
            BrakeWork += torque * (before + Speed) / 2 * _step;
            if (_direction == 0 && Math.Abs(Speed) > 1e-9)
            {
                _direction = Math.Sign(Speed);
            }

            Reversed |= Speed * _direction < -1e-9;
        }
    }
}
