using NodeRunner.Domain;
using NodeRunner.Theme;

namespace NodeRunner.Ui.Tests;

public sealed class ServoPartTests
{
    [Theory]
    [InlineData(52, 24, false, 8)]
    [InlineData(52, 44, false, 0)]
    [InlineData(70, 0, true, 0)]
    [InlineData(20, 0, false, 8)]
    [InlineData(9, 0, false, 0)]
    public void HousingReachFor_LeavesRoomForSensorsAndPistonCylinders(
        float freeLength,
        float sensorLength,
        bool pistonCylinderEnd,
        float expected)
    {
        ServoPart.HousingReachFor(freeLength, sensorLength, pistonCylinderEnd).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.25)]
    [InlineData(1)]
    public void BandSweep_MatchesTheModelStops_InClockwiseGodotRotation(double start)
    {
        const double builtAngle = 0.4;
        var servo = new ServoDef(1, 2, 3, 4).WithSettings(ServoDef.DefaultStrength, Math.PI / 2, start, ServoDef.DefaultMaxSpeed, ServoDef.DefaultRiseTime);

        var (from, to) = ServoPart.BandSweep(-(float)builtAngle, (float)servo.Range, (float)servo.Start);

        from.ShouldBe(-(float)Mechanics.Servo.UpperAngle(servo, builtAngle), 1e-5f);
        to.ShouldBe(-(float)Mechanics.Servo.LowerAngle(servo, builtAngle), 1e-5f);
    }
}
