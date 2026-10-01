namespace NodeRunner.Domain.Tests;

public sealed class AccelerometerTests
{
    [Fact]
    public void ReadingNames_AreAlongThenAcross()
    {
        Accelerometer.ReadingNames.ShouldBe(["along", "across"]);
    }

    [Fact]
    public void Rest_OnLevelBeam_ReadsOneGUp()
    {
        var frameForce = Accelerometer.ToSensorFrame(new Vector2D(0, -1), beamRotation: 0, upSign: -1);
        var reading = Accelerometer.Reading(Accelerometer.Rest(frameForce));

        reading.X.ShouldBe(0, 1e-12);
        reading.Y.ShouldBe(Math.Tanh(1), 1e-12);
    }

    [Fact]
    public void Rest_OnTiltedBeam_TurnsGravityReading()
    {
        var frameForce = Accelerometer.ToSensorFrame(new Vector2D(0, -1), beamRotation: -Math.PI / 4, upSign: -1);
        var reading = Accelerometer.Reading(Accelerometer.Rest(frameForce));

        reading.X.ShouldBe(Math.Tanh(Math.Sqrt(0.5)), 1e-12);
        reading.Y.ShouldBe(Math.Tanh(Math.Sqrt(0.5)), 1e-12);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void Step_WithConstantInput_SettlesToNewRest(int timeScale)
    {
        var force = new Vector2D(0, 1);
        var state = Accelerometer.Rest(new Vector2D(0, 0));

        for (var i = 0; i < 600 / timeScale; i++)
        {
            state = Accelerometer.Step(state, force, timeScale / 60.0);
        }

        var reading = Accelerometer.Reading(state);
        reading.X.ShouldBe(0, 0.01);
        reading.Y.ShouldBe(Math.Tanh(1), 0.01);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void Step_AfterImpulse_ProducesSpikeThatDecays(int timeScale)
    {
        var restForce = new Vector2D(0, 1);
        var state = Accelerometer.Rest(restForce);

        state = Accelerometer.Step(state, new Vector2D(5, 1), timeScale / 60.0);
        var spike = Accelerometer.Reading(state);

        for (var i = 0; i < 240 / timeScale; i++)
        {
            state = Accelerometer.Step(state, restForce, timeScale / 60.0);
        }

        var settled = Accelerometer.Reading(state);
        spike.X.ShouldBeGreaterThan(0.05);
        settled.X.ShouldBe(0, 0.01);
        settled.Y.ShouldBe(Math.Tanh(1), 0.01);
    }

    [Fact]
    public void Reading_ForHugeDisplacement_StaysInUnitRange()
    {
        var reading = Accelerometer.Reading(new ProofMass(new Vector2D(1_000_000, -1_000_000), new Vector2D(0, 0)));

        reading.X.ShouldBeInRange(-1, 1);
        reading.Y.ShouldBeInRange(-1, 1);
    }

    [Fact]
    public void Step_WithSameInputs_IsDeterministic()
    {
        var left = Accelerometer.Rest(new Vector2D(0, 1));
        var right = Accelerometer.Rest(new Vector2D(0, 1));

        for (var i = 0; i < 60; i++)
        {
            var force = new Vector2D(Math.Sin(i * 0.1), 1 + Math.Cos(i * 0.2));
            left = Accelerometer.Step(left, force, 1.0 / 60.0);
            right = Accelerometer.Step(right, force, 1.0 / 60.0);
        }

        right.ShouldBe(left);
    }

    [Fact]
    public void Step_WithNonPositiveDt_Throws()
    {
        Action action = () => Accelerometer.Step(Accelerometer.Rest(new Vector2D(0, 1)), new Vector2D(0, 1), 0);

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0, 0, 2, 0, -1)]
    [InlineData(2, 0, 0, 0, 1)]
    [InlineData(0, 0, 0, 2, -1)]
    public void UpSign_IsFixedFromBuiltPose(double ax, double ay, double bx, double by, int expected)
    {
        Accelerometer.UpSign(new Vector2D(ax, ay), new Vector2D(bx, by)).ShouldBe(expected);
    }

    [Fact]
    public void ToSensorFrame_LevelBeam_MapsWorldUpToAcrossUp()
    {
        var frame = Accelerometer.ToSensorFrame(new Vector2D(0, -1), beamRotation: 0, upSign: -1);

        frame.X.ShouldBe(0, 1e-12);
        frame.Y.ShouldBe(1, 1e-12);
    }

    [Fact]
    public void ToSensorFrame_TiltedBeam_SplitsWorldUpAcrossAxes()
    {
        var frame = Accelerometer.ToSensorFrame(new Vector2D(0, -1), beamRotation: -Math.PI / 4, upSign: -1);

        frame.X.ShouldBe(Math.Sqrt(0.5), 1e-12);
        frame.Y.ShouldBe(Math.Sqrt(0.5), 1e-12);
    }

    [Fact]
    public void ToSensorFrame_ReversedLevelBeam_StillMapsWorldUpToAcrossUp()
    {
        var frame = Accelerometer.ToSensorFrame(new Vector2D(0, -1), beamRotation: Math.PI, upSign: 1);

        frame.X.ShouldBe(0, 1e-12);
        frame.Y.ShouldBe(1, 1e-12);
    }

    [Fact]
    public void ToSensorFrame_VerticalBeam_UsesDeterministicTieBreak()
    {
        var frame = Accelerometer.ToSensorFrame(new Vector2D(0, -1), beamRotation: Math.PI / 2, upSign: -1);

        frame.X.ShouldBe(-1, 1e-12);
        frame.Y.ShouldBe(0, 1e-12);
    }
}
