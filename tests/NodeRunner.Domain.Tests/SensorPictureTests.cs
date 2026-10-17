namespace NodeRunner.Domain.Tests;

public sealed class SensorPictureTests
{
    private const double _hub = NodeDef.HubRadius;
    private const double _motor = JointMotor.Radius;

    // The kind's square at the beam's midpoint, turned with the beam, a bit inside and outside each edge.
    [Theory]
    [InlineData(SensorKind.Accelerometer)]
    [InlineData(SensorKind.Camera)]
    public void Contains_UsesTheKindsSquareAtTheMidpointTurnedWithTheBeam(SensorKind kind)
    {
        var half = SensorPicture.SizeOf(kind) / 2;

        new[] { new Vector2D(half - 1, 50), new(half + 1, 50), new(0, 50 + half - 1), new(0, 50 + half + 1) }
            .Select(at => SensorPicture.Contains(kind, at, new Vector2D(0, 0), new Vector2D(0, 100), _hub, _hub)).ShouldBe([true, false, true, false]);
    }

    // A motor (27) on A and a hub (15) on B, 200 apart: edges at 27 and 185, so the picture shifts 6 towards B.
    [Fact]
    public void Centre_SitsMidwayBetweenWhatCoversEachEnd()
    {
        (SensorPicture.Shift(_hub, _hub), SensorPicture.Shift(_motor, _hub), SensorPicture.Shift(_hub, _motor)).ShouldBe((0, 6, -6));
        var centre = SensorPicture.Centre(new Vector2D(0, 0), new Vector2D(200, 0), _motor, _hub);
        (centre.X, centre.Y).ShouldBe((106, 0));
        SensorPicture.Contains(SensorKind.Accelerometer, new Vector2D(0, 106), new Vector2D(0, 0), new Vector2D(0, 200), _motor, _hub).ShouldBeTrue();
        SensorPicture.Contains(SensorKind.Accelerometer, new Vector2D(0, 93), new Vector2D(0, 0), new Vector2D(0, 200), _motor, _hub).ShouldBeFalse();
        SensorPicture.Contains(SensorKind.Accelerometer, new Vector2D(12, 12), new Vector2D(10, 10), new Vector2D(10, 10), _hub, _hub).ShouldBeTrue();
        SensorPicture.SizeOf(SensorKind.Camera).ShouldBeGreaterThan(1.8 * SensorPicture.SizeOf(SensorKind.Accelerometer));
    }
}
