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
        var sensor = new SensorDef(3, 2, SensorKind.Camera, "Eye", 0.5, 5, 1, 300);

        sensor.WithName("Look").ShouldBe(new SensorDef(3, 2, SensorKind.Camera, "Look", 0.5, 5, 1, 300));
        sensor.WithAim(-1).ShouldBe(new SensorDef(3, 2, SensorKind.Camera, "Eye", -1, 5, 1, 300));
        sensor.WithRays(1).ShouldBe(new SensorDef(3, 2, SensorKind.Camera, "Eye", 0.5, 1, 1, 300));
        sensor.WithSpread(0.3).ShouldBe(new SensorDef(3, 2, SensorKind.Camera, "Eye", 0.5, 5, 0.3, 300));
        sensor.WithRange(150).ShouldBe(new SensorDef(3, 2, SensorKind.Camera, "Eye", 0.5, 5, 1, 150));
        sensor.OnBeam(7, 0.2).ShouldBe(new SensorDef(3, 7, SensorKind.Camera, "Eye", 0.2, 5, 1, 300));
    }

    [Fact]
    public void Camera_WithoutSettings_GetsTodaysFan()
    {
        var camera = new SensorDef(3, 2, SensorKind.Camera);

        camera.Rays.ShouldBe(3);
        camera.Spread.ShouldBe(Math.PI / 2);
        camera.Range.ShouldBe(220);
        camera.ShouldBe(new SensorDef(3, 2, SensorKind.Camera, rays: 3, spread: Math.PI / 2, range: 220));
    }

    [Fact]
    public void Accelerometer_HasNoCameraSettings()
    {
        var accelerometer = new SensorDef(3, 2, SensorKind.Accelerometer);

        accelerometer.Rays.ShouldBeNull();
        accelerometer.Spread.ShouldBeNull();
        accelerometer.Range.ShouldBeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Constructor_WithAnOddRayCountUpToFive_StoresIt(int rays)
    {
        new SensorDef(3, 2, SensorKind.Camera, rays: rays).Rays.ShouldBe(rays);
        SensorDef.IsRayCount(rays).ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(-1)]
    public void Constructor_WithAnotherRayCount_Throws(int rays)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SensorDef(3, 2, SensorKind.Camera, rays: rays));
        SensorDef.IsRayCount(rays).ShouldBeFalse();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(4.0)]
    [InlineData(double.NaN)]
    public void Constructor_WithASpreadOutsideAHalfTurn_Throws(double spread)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SensorDef(3, 2, SensorKind.Camera, spread: spread));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-10.0)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_WithoutAPositiveRange_Throws(double range)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SensorDef(3, 2, SensorKind.Camera, range: range));
    }

    [Fact]
    public void Constructor_WithCameraSettingsOnAnAccelerometer_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SensorDef(3, 2, SensorKind.Accelerometer, rays: 3));
        Should.Throw<ArgumentOutOfRangeException>(() => new SensorDef(3, 2, SensorKind.Accelerometer, spread: 1));
        Should.Throw<ArgumentOutOfRangeException>(() => new SensorDef(3, 2, SensorKind.Accelerometer, range: 100));
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
