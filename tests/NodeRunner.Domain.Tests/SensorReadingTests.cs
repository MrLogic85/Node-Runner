using System.Text.Json;

namespace NodeRunner.Domain.Tests;

public sealed class SensorReadingTests
{
    [Fact]
    public void Equality_WithSameValues_HoldsByValue()
    {
        var a = new SensorReading("Accelerometer", 1, "across", 0.5);
        var b = new SensorReading("Accelerometer", 1, "across", 0.5);

        a.ShouldBe(b);
    }

    [Fact]
    public void JsonRoundTrip_PreservesValue()
    {
        var reading = new SensorReading("Accelerometer", 1, "across", 0.5);

        var roundTripped = JsonSerializer.Deserialize<SensorReading>(JsonSerializer.Serialize(reading));

        roundTripped.ShouldBe(reading);
    }
}
