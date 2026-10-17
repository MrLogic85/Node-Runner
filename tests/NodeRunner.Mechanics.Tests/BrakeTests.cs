namespace NodeRunner.Mechanics.Tests;

public sealed class BrakeTests
{
    private const double _step = 1.0 / 60;
    private const double _inertia = 2000;
    private const double _strength = 2_000_000;

    // Capped at its strength, uncapped at infinity, nothing at rest; a weak one slows a free joint at its strength.
    [Fact]
    public void ItsTorque_OpposesTheSpeed_UpToItsStrength()
    {
        Brake.Next(speed: 100, _strength, null, _step, _inertia).Torque.ShouldBe(-_strength);
        Brake.Next(speed: -100, _strength, null, _step, _inertia).Torque.ShouldBe(_strength);
        Brake.Next(speed: 1e6, double.PositiveInfinity, null, _step, _inertia).Torque.ShouldBe(-1e6 * _inertia / _step);
        Brake.Next(speed: 0, _strength, null, _step, _inertia).Torque.ShouldBe(0);
        var joint = new Joint(speed: 30);
        joint.Step(load: 0);
        joint.Speed.ShouldBe(30 - (_strength * _step / _inertia), 1e-9);

        foreach (var speed in new[] { 3.0, -3 })
        {
            var free = new Joint(speed);
            free.Run(steps: 120, load: 0);
            free.Speed.ShouldBe(0, 1e-9);
            free.Reversed.ShouldBeFalse();
        }
    }

    // Within its strength it holds a sudden load still: at once with the right inertia guess, and in time with one
    // from 0.3 to 1.3 times the real inertia.
    [Theory]
    [InlineData(0.2, 1, 10, 1e-9, 1e-3)]
    [InlineData(0.9, 1, 10, 1e-9, 1e-3)]
    [InlineData(-0.9, 1, 10, 1e-9, 1e-3)]
    [InlineData(0.5, 0.3, 300, 1e-6, 1)]
    [InlineData(0.5, 1.3, 300, 1e-6, 1)]
    public void ALoadWithinStrength_IsHeld(double share, double estimateShare, int settleSteps, double angleTolerance, double torqueTolerance)
    {
        var joint = new Joint(speed: 0, estimate: estimateShare * _inertia);

        joint.Run(settleSteps, load: share * _strength);
        var settled = joint.Angle;
        joint.Run(steps: 300, load: share * _strength);

        (joint.Angle - settled).ShouldBe(0, angleTolerance);
        joint.Brake!.Value.Torque.ShouldBe(-share * _strength, torqueTolerance);
    }

    // Past its strength it slips, braked by the full strength; as the load eases it grips again, never turning back.
    [Theory]
    [InlineData(0.5)]
    [InlineData(0)]
    public void ALoadPastStrength_Slips_ThenItGripsAgain(double easedShare)
    {
        var joint = new Joint(speed: 0);
        joint.Run(steps: 10, load: 1.5 * _strength);
        var before = joint.Speed;
        joint.Step(load: 1.5 * _strength);
        joint.Brake!.Value.Torque.ShouldBe(-_strength);
        (joint.Speed - before).ShouldBe(0.5 * _strength * _step / _inertia, 1e-9);

        joint.Run(steps: 30, load: easedShare * _strength);
        var settled = joint.Angle;
        joint.Run(steps: 120, load: easedShare * _strength);

        joint.Reversed.ShouldBeFalse();
        joint.Speed.ShouldBe(0, 1e-9);
        (joint.Angle - settled).ShouldBe(0, 1e-9);
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
        joint.Speed.ShouldBe(0, 1e-6);
    }

    [Fact]
    public void ItRefuses_AStrengthStepOrInertiaThatIsNotPositive()
    {
        foreach (var strength in new[] { 0, -1, double.NaN, double.NegativeInfinity })
        {
            Should.Throw<ArgumentOutOfRangeException>(() => Brake.Next(0, strength, null, _step, _inertia));
        }

        foreach (var value in new[] { 0, double.NaN, double.PositiveInfinity })
        {
            Should.Throw<ArgumentOutOfRangeException>(() => Brake.Next(0, _strength, null, value, _inertia));
            Should.Throw<ArgumentOutOfRangeException>(() => Brake.Next(0, _strength, null, _step, value));
        }
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
