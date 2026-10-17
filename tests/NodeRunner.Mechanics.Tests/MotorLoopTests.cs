namespace NodeRunner.Mechanics.Tests;

public sealed class MotorLoopTests
{
    private const double _step = 1.0 / 60;
    private const double _strength = 500_000;
    private const double _riseTime = 0.2;
    private const double _maxSpeed = Math.Tau;

    // Heavy enough that the push part always asks for more than the rise time allows.
    private const double _heavy = 1e9;

    // From rest it builds towards the wanted speed at the rise rate, reaching full within about its rise time and
    // never more than the chosen torque; past its target it drops its push at once.
    [Fact]
    public void ItsTorque_BuildsAtTheRiseRate_UpToTheChosenTorque()
    {
        MotorLoop.SpeedInput(0, _maxSpeed).ShouldBe(0);
        MotorLoop.SpeedInput(_maxSpeed, _maxSpeed).ShouldBe(Math.Tanh(1), 1e-12);
        MotorLoop.SpeedInput(-1000 * _maxSpeed, _maxSpeed).ShouldBe(-1, 1e-9);

        var forward = Next(wantedSpeed: 1, _strength, _riseTime, default);
        forward.Torque.ShouldBe(_strength * _step / _riseTime, 1e-6);
        Next(wantedSpeed: -1, _strength, _riseTime, default).Torque.ShouldBe(-forward.Torque, 1e-6);
        Next(wantedSpeed: 1, _strength, riseTime: 0.5, default).Torque.ShouldBe(Next(wantedSpeed: 1, _strength, riseTime: 0.1, default).Torque / 5, 1e-6);
        Next(wantedSpeed: 1, chosenTorque: 0, _riseTime, default).Torque.ShouldBe(0);
        var capped = Next(wantedSpeed: 1, 0.2 * _strength, _riseTime, new MotorState(_strength, _strength));
        Math.Abs(capped.Torque).ShouldBeLessThanOrEqualTo(0.2 * _strength);
        Math.Abs(capped.Holding).ShouldBeLessThanOrEqualTo(0.2 * _strength);

        var motor = default(MotorState);
        for (var step = 0; step < (int)(1.1 * _riseTime / _step); step++)
        {
            motor = Next(wantedSpeed: 1, _strength, _riseTime, motor);
        }

        motor.Torque.ShouldBe(_strength);

        var past = MotorLoop.Next(wantedSpeed: -_maxSpeed, speed: 0, _strength, _riseTime, new MotorState(_strength, 0), _step, effectiveInertia: 1000);
        past.Torque.ShouldBeInRange(-_strength * _step / _riseTime, -double.Epsilon);
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

        speed.ShouldBe(wantedSpeed, 0.01 * _maxSpeed);
        motor.Torque.ShouldBe(-load, 0.01 * _strength);
    }

    [Fact]
    public void ItRefuses_AnInertiaOrStepThatIsNotFiniteAndPositive()
    {
        foreach (var (effectiveInertia, step, parameter) in new[]
        {
            (0, _step, "effectiveInertia"), (-1, _step, "effectiveInertia"), (double.NaN, _step, "effectiveInertia"),
            (double.PositiveInfinity, _step, "effectiveInertia"), (1000, 0, "step"), (1000, -_step, "step"), (1000, double.NaN, "step"),
        })
        {
            Should.Throw<ArgumentOutOfRangeException>(() => MotorLoop.Next(wantedSpeed: 1, speed: 0, _strength, _riseTime, default, step, effectiveInertia))
                .ParamName.ShouldBe(parameter);
        }
    }

    private static MotorState Next(double wantedSpeed, double chosenTorque, double riseTime, MotorState previous) =>
        MotorLoop.Next(wantedSpeed, speed: 0, chosenTorque, riseTime, previous, _step, _heavy);
}
