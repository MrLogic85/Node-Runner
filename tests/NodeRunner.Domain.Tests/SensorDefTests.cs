namespace NodeRunner.Domain.Tests;

public sealed class SensorDefTests
{
    [Fact]
    public void Constructor_WithValidValues_StoresValues()
    {
        var sensor = new SensorDef(3, 2, SensorKind.Accelerometer, "Tilt");

        sensor.Id.ShouldBe(3);
        sensor.BeamId.ShouldBe(2);
        sensor.Kind.ShouldBe(SensorKind.Accelerometer);
        sensor.Name.ShouldBe("Tilt");
    }

    [Fact]
    public void Constructor_WithInvalidBeamId_Throws()
    {
        var action = () => new SensorDef(3, -1, SensorKind.Accelerometer);

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithUndefinedKind_Throws()
    {
        var action = () => new SensorDef(3, 2, (SensorKind)99);

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithCameraAim_StoresIt()
    {
        new SensorDef(3, 2, SensorKind.Camera, aim: 0.5).Aim.ShouldBe(0.5);
    }

    [Fact]
    public void Constructor_WithAimOnAccelerometer_Throws()
    {
        var action = () => new SensorDef(3, 2, SensorKind.Accelerometer, aim: 0.5);

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WithNonFiniteAim_Throws(double aim)
    {
        var action = () => new SensorDef(3, 2, SensorKind.Camera, aim: aim);

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void With_KeepTheOtherValues()
    {
        var sensor = new SensorDef(3, 2, SensorKind.Camera, "Eye", 0.5);

        sensor.WithBeam(4).ShouldBe(new SensorDef(3, 4, SensorKind.Camera, "Eye", 0.5));
        sensor.WithName("Look").ShouldBe(new SensorDef(3, 2, SensorKind.Camera, "Look", 0.5));
        sensor.WithAim(-1).ShouldBe(new SensorDef(3, 2, SensorKind.Camera, "Eye", -1));
    }
}
