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

    [Theory]
    [InlineData(10, 0, 0)]
    [InlineData(0, 10, -Math.PI / 2)]
    [InlineData(0, -10, Math.PI / 2)]
    [InlineData(10, 10, -Math.PI / 4)]
    public void DefaultAim_UndoesTheBeamsTurn(double dx, double dy, double expected) =>
        SensorDef.DefaultAim(new Vector2D(3, 4), new Vector2D(3 + dx, 4 + dy)).ShouldBe(expected, 1e-12);

    [Fact]
    public void DefaultAim_StaysWithinAHalfTurn() =>
        Math.Abs(SensorDef.DefaultAim(new Vector2D(0, 0), new Vector2D(-10, 0))).ShouldBe(Math.PI, 1e-12);

    [Fact]
    public void DefaultAim_OnAZeroLengthBeam_IsZero() =>
        SensorDef.DefaultAim(new Vector2D(1, 1), new Vector2D(1, 1)).ShouldBe(0);
}
