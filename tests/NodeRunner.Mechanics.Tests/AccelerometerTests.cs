using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class AccelerometerTests
{
    private static readonly double _half = Math.Sqrt(0.5);

    // World up (0, −1) in the sensor's frame: across-up on a level beam either way it was built, split on a beam
    // rising 45° to the right, and the tie break on a vertical one. At rest it reads 1 g up, through tanh.
    [Fact]
    public void AtRest_ItReadsOneGUp_TurnedWithItsBeam()
    {
        foreach (var (rotation, upSign, x, y) in new[] { (0.0, -1, 0.0, 1.0), (Math.PI, 1, 0, 1), (-Math.PI / 4, -1, _half, _half), (Math.PI / 2, -1, -1, 0) })
        {
            var frame = Accelerometer.ToSensorFrame(new Vector2D(0, -1), rotation, upSign);
            Near(frame, x, y, $"rotation {rotation}");
            Near(Accelerometer.Reading(Accelerometer.Rest(frame)), Math.Tanh(x), Math.Tanh(y), $"rotation {rotation}");
        }

        // The glyph's weight sits half its travel down, sliding to the lower end of a slope, the same either way built.
        foreach (var (rotation, upSign, x, y) in new[] { (0.0, -1, 0.0, 1.0), (Math.PI, 1, 0, 1), (-Math.PI / 4, -1, -_half, _half) })
        {
            Near(Accelerometer.RestWeightOffset(rotation, upSign), Accelerometer.WeightTravelPerG * x, Accelerometer.WeightTravelPerG * y, $"rotation {rotation}");
        }

        Accelerometer.SpecificForce(new Vector2D(0, 0), 980).ShouldBe(new Vector2D(0, -1));
        Accelerometer.SpecificForce(new Vector2D(490, 980), 980).ShouldBe(new Vector2D(0.5, 0));
        foreach (var (a, b, upSign) in new[] { (new Vector2D(0, 0), new Vector2D(2, 0), -1), (new(2, 0), new(0, 0), 1), (new(0, 0), new(0, 2), -1) })
        {
            Accelerometer.UpSign(a, b).ShouldBe(upSign);
        }
    }

    // Its weight settles to the new rest under any physics step, spikes after a jolt and settles back, the same every run.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void ItsWeight_SettlesToRest_AndSpikesAfterAJolt(int ticksPerStep)
    {
        var dt = ticksPerStep / 60.0;
        var up = new Vector2D(0, 1);
        var state = Accelerometer.Rest(new Vector2D(0, 0));
        for (var i = 0; i < 600 / ticksPerStep; i++)
        {
            state = Accelerometer.Step(state, up, dt);
        }

        Near(Accelerometer.Reading(state), 0, Math.Tanh(1), "settled", 0.01);

        state = Accelerometer.Step(Accelerometer.Rest(up), new Vector2D(5, 1), dt);
        Accelerometer.Reading(state).X.ShouldBeGreaterThan(0.05);
        var again = state;
        for (var i = 0; i < 240 / ticksPerStep; i++)
        {
            var force = new Vector2D(0.01 * Math.Sin(i), 1);
            state = Accelerometer.Step(state, force, dt);
            again = Accelerometer.Step(again, force, dt);
        }

        Near(Accelerometer.Reading(state), 0, Math.Tanh(1), "after the jolt", 0.01);
        again.ShouldBe(state);
    }

    [Fact]
    public void FarOff_ItsReadingAndWeight_StayInTheFrame()
    {
        var reading = Accelerometer.Reading(new ProofMass(new Vector2D(1_000_000, -1_000_000), new Vector2D(0, 0)));
        reading.X.ShouldBeInRange(-1, 1);
        reading.Y.ShouldBeInRange(-1, 1);
        var far = 10 * Accelerometer.ReferenceDisplacement;
        Accelerometer.WeightOffset(new ProofMass(new Vector2D(far, far), new Vector2D(0, 0))).ShouldBe(new Vector2D(1, -1));
    }

    [Fact]
    public void ItRefuses_NoGravity_NoTime_AndABadFrame()
    {
        foreach (var gravity in new[] { 0, -1, double.NaN })
        {
            Should.Throw<ArgumentOutOfRangeException>(() => Accelerometer.SpecificForce(new Vector2D(0, 0), gravity));
        }

        Should.Throw<ArgumentOutOfRangeException>(() => Accelerometer.Step(Accelerometer.Rest(new Vector2D(0, 1)), new Vector2D(0, 1), 0));
        Should.Throw<ArgumentException>(() => Accelerometer.ToSensorFrame(new Vector2D(0, -1), 0, upSign: 0));
        Should.Throw<ArgumentException>(() => Accelerometer.ToSensorFrame(new Vector2D(0, -1), double.NaN, upSign: 1));
    }

    private static void Near(Vector2D actual, double x, double y, string row, double tolerance = 1e-12)
    {
        actual.X.ShouldBe(x, tolerance, row);
        actual.Y.ShouldBe(y, tolerance, row);
    }
}
