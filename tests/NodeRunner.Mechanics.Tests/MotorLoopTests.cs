namespace NodeRunner.Mechanics.Tests;

public sealed class MotorLoopTests
{
    private const double _step = 1.0 / 60;
    private const double _strength = 500_000;
    private const double _riseTime = 0.2;
    private const double _maxSpeed = Math.Tau;

    // Heavy enough that the push part always asks for more than the rise time allows.
    private const double _heavy = 1e9;

    [Fact]
    public void SpeedInput_IsPositiveCounterClockwise_AndSaturatesSoftly()
    {
        MotorLoop.SpeedInput(0, _maxSpeed).ShouldBe(0);
        MotorLoop.SpeedInput(_maxSpeed, _maxSpeed).ShouldBe(Math.Tanh(1), tolerance: 1e-12);
        MotorLoop.SpeedInput(-1000 * _maxSpeed, _maxSpeed).ShouldBe(-1, tolerance: 1e-9);
    }

    [Fact]
    public void Next_FromRest_BuildsTowardsTheWantedSpeed_AtTheRiseRate()
    {
        var forward = Next(wantedSpeed: 1, _strength, _riseTime, default);
        var back = Next(wantedSpeed: -1, _strength, _riseTime, default);

        forward.Torque.ShouldBe(_strength * _step / _riseTime, tolerance: 1e-6);
        back.Torque.ShouldBe(-forward.Torque, tolerance: 1e-6);
    }

    [Fact]
    public void Next_WithNoChosenTorque_IsZero() =>
        Next(wantedSpeed: 1, chosenTorque: 0, _riseTime, default).Torque.ShouldBe(0);

    [Fact]
    public void Next_NeverExceedsTheChosenTorque()
    {
        var chosen = 0.2 * _strength;

        var motor = Next(wantedSpeed: 1, chosen, _riseTime, new MotorState(_strength, _strength));

        Math.Abs(motor.Torque).ShouldBeLessThanOrEqualTo(chosen);
        Math.Abs(motor.Holding).ShouldBeLessThanOrEqualTo(chosen);
    }

    [Fact]
    public void Next_ReachesFullWithinAboutRiseTime()
    {
        var motor = default(MotorState);
        for (var step = 0; step < (int)(1.1 * _riseTime / _step); step++)
        {
            motor = Next(wantedSpeed: 1, _strength, _riseTime, motor);
        }

        motor.Torque.ShouldBe(_strength);
    }

    [Fact]
    public void Next_ALongerRiseTime_BuildsMoreSlowly()
    {
        var quickTorque = Next(wantedSpeed: 1, _strength, riseTime: 0.1, default).Torque;
        var slowTorque = Next(wantedSpeed: 1, _strength, riseTime: 0.5, default).Torque;

        slowTorque.ShouldBe(quickTorque / 5, tolerance: 1e-6);
    }

    [Fact]
    public void Next_PastItsTarget_DropsItsPushAtOnce()
    {
        var pushing = new MotorState(_strength, 0);

        var motor = MotorLoop.Next(wantedSpeed: -_maxSpeed, speed: 0, _strength, _riseTime, pushing, _step, effectiveInertia: 1000);

        motor.Torque.ShouldBeLessThan(0);
        motor.Torque.ShouldBeGreaterThanOrEqualTo(-_strength * _step / _riseTime);
    }

    [Theory]
    [InlineData(340)]
    [InlineData(30000)]
    public void Next_KeepsTheWantedSpeed_EvenWhenItsInertiaGuessIsFarOff(double realInertia)
    {
        var wantedSpeed = 0.5 * _maxSpeed;
        var load = -0.5 * _strength;

        var speed = 0.0;
        var motor = default(MotorState);
        for (var step = 0; step < 1800; step++)
        {
            motor = MotorLoop.Next(wantedSpeed, speed, _strength, _riseTime, motor, _step, effectiveInertia: 1000);
            speed += (motor.Torque + load) / realInertia * _step;
        }

        speed.ShouldBe(wantedSpeed, tolerance: 0.01 * _maxSpeed);
        motor.Torque.ShouldBe(-load, tolerance: 0.01 * _strength);
    }

    [Theory]
    [InlineData(0, _step, "effectiveInertia")]
    [InlineData(-1, _step, "effectiveInertia")]
    [InlineData(double.NaN, _step, "effectiveInertia")]
    [InlineData(double.PositiveInfinity, _step, "effectiveInertia")]
    [InlineData(1000, 0, "step")]
    [InlineData(1000, -_step, "step")]
    [InlineData(1000, double.NaN, "step")]
    public void Next_WithoutAFinitePositiveInertiaAndStep_Throws(double effectiveInertia, double step, string parameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => MotorLoop.Next(wantedSpeed: 1, speed: 0, _strength, _riseTime, default, step, effectiveInertia));

        exception.ParamName.ShouldBe(parameter);
    }

    private static MotorState Next(double wantedSpeed, double chosenTorque, double riseTime, MotorState previous) =>
        MotorLoop.Next(wantedSpeed, speed: 0, chosenTorque, riseTime, previous, _step, _heavy);
}
