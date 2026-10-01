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
}
