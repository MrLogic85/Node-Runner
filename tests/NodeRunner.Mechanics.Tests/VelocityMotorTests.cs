using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class VelocityMotorTests
{
    private const double _step = 1.0 / 60;

    private static readonly VelocityMotorDef _motor = new(1, 2, 3, 4);

    // Heavy enough that the push part always asks for more than the rise time allows.
    private const double _heavy = 1e9;

    // Its angle inputs are the sine and cosine of the angle between its sides, with no jump past half a turn. A Wheel
    // target turned a quarter turn counter-clockwise (−π/2 in Godot, y-down) from a fixed side pointing right reads 1, 0.
    [Fact]
    public void ItsAngleInputs_ReadTheAngleBetweenTheSides()
    {
        foreach (var (angle, sin, cos) in new[] { (0.0, 0.0, 1.0), (Math.PI / 2, 1, 0), (Math.PI, 0, -1), (-Math.PI / 2, -1, 0), ((2 * Math.PI) + (Math.PI / 2), 1, 0) })
        {
            var inputs = VelocityMotor.AngleInputs(angle);
            inputs.Sin.ShouldBe(sin, 1e-12);
            inputs.Cos.ShouldBe(cos, 1e-12);
        }

        var wheel = VelocityMotor.AngleInputs(HingePartSideGeometry.WheelAngle(bodyRotation: -Math.PI / 2) - HingePartSideGeometry.LinkAngle(new Vector2D(0, 0), new Vector2D(10, 0)));
        wheel.Sin.ShouldBe(1, 1e-12);
        wheel.Cos.ShouldBe(0, 1e-12);
        var before = VelocityMotor.AngleInputs(Math.PI - 1e-6);
        var after = VelocityMotor.AngleInputs(-Math.PI + 1e-6);
        before.Sin.ShouldBe(after.Sin, 1e-5);
        before.Cos.ShouldBe(after.Cos, 1e-5);
    }

    // It wants its output times Max speed, clamped; from rest it builds that way at the rise rate.
    [Fact]
    public void ItsTorque_BuildsTowardsTheWantedSpeed_AtTheRiseRate()
    {
        foreach (var (output, share) in new[] { (0.5, 0.5), (-0.25, -0.25), (3, 1), (-3, -1) })
        {
            VelocityMotor.WantedSpeed(output, _motor).ShouldBe(share * _motor.MaxSpeed);
        }

        var forward = Next(speedOutput: 1, strength: 1);
        forward.Torque.ShouldBe(_motor.Strength * _step / _motor.RiseTime, 1e-6);
    }

    // Against a load past its strength it gives all it can and is pushed back.
    [Fact]
    public void AgainstALoadPastItsStrength_ItGivesAllItCan_AndIsPushedBack()
    {
        var (speed, torque) = Run(speedOutput: 0.5, load: -2 * _motor.Strength);

        torque.ShouldBe(_motor.Strength);
        speed.ShouldBeLessThan(0);
    }

    private static MotorState Next(double speedOutput, double strength) =>
        VelocityMotor.NextMotor(_motor, speed: 0, speedOutput, strength, default, _step, _heavy);

    private static (double Speed, double Torque) Run(double speedOutput, double load)
    {
        const double inertia = 1385;
        var speed = 0.0;
        var motor = default(MotorState);
        for (var step = 0; step < 1800; step++)
        {
            motor = VelocityMotor.NextMotor(_motor, speed, speedOutput, strength: 1, motor, _step, effectiveInertia: inertia);
            speed += (motor.Torque + load) / inertia * _step;
        }

        return (speed, motor.Torque);
    }
}
