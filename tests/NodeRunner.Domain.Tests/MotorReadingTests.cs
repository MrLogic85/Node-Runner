using System.Text.Json;

namespace NodeRunner.Domain.Tests;

public sealed class MotorReadingTests
{
    [Fact]
    public void Equality_WithSameValues_HoldsByValue()
    {
        var a = new MotorReading(1, 0.75, 1200);
        var b = new MotorReading(1, 0.75, 1200);

        a.ShouldBe(b);
    }

    [Fact]
    public void JsonRoundTrip_PreservesValue()
    {
        var reading = new MotorReading(1, 0.75, 1200);

        var roundTripped = JsonSerializer.Deserialize<MotorReading>(JsonSerializer.Serialize(reading));

        roundTripped.ShouldBe(reading);
    }
}
