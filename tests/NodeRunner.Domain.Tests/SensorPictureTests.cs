namespace NodeRunner.Domain.Tests;

public sealed class SensorPictureTests
{
    private const double _hub = NodeDef.HubRadius;
    private const double _servo = JointMotor.Radius;

    [Theory]
    [InlineData(SensorKind.Accelerometer)]
    [InlineData(SensorKind.Camera)]
    public void Contains_UsesTheKindsSquareAtTheMidpointTurnedWithTheBeam(SensorKind kind)
    {
        var nodeA = new Vector2D(0, 0);
        var nodeB = new Vector2D(0, 100);
        var half = SensorPicture.SizeOf(kind) / 2;

        SensorPicture.Contains(kind, new Vector2D(half - 1, 50), nodeA, nodeB, _hub, _hub).ShouldBeTrue();
        SensorPicture.Contains(kind, new Vector2D(half + 1, 50), nodeA, nodeB, _hub, _hub).ShouldBeFalse();
        SensorPicture.Contains(kind, new Vector2D(0, 50 + half - 1), nodeA, nodeB, _hub, _hub).ShouldBeTrue();
        SensorPicture.Contains(kind, new Vector2D(0, 50 + half + 1), nodeA, nodeB, _hub, _hub).ShouldBeFalse();
    }

    [Fact]
    public void Contains_OnAZeroLengthBeam_StillHasAPicture()
    {
        var node = new Vector2D(10, 10);

        SensorPicture.Contains(SensorKind.Accelerometer, new Vector2D(12, 12), node, node, _hub, _hub).ShouldBeTrue();
    }

    [Fact]
    public void Shift_IsHalfTheDifferenceOfWhatCoversEachEnd()
    {
        SensorPicture.Shift(_hub, _hub).ShouldBe(0);
        SensorPicture.Shift(_servo, _hub).ShouldBe(6);
        SensorPicture.Shift(_hub, _servo).ShouldBe(-6);
    }

    [Fact]
    public void Centre_SitsMidwayBetweenTheServosEdgeAndTheOtherHub()
    {
        // A Servo (27) on A and a hub (15) on B, 200 apart: edges at 27 and 185.
        var centre = SensorPicture.Centre(new Vector2D(0, 0), new Vector2D(200, 0), _servo, _hub);

        centre.X.ShouldBe((27 + 185) / 2.0, tolerance: 1e-9);
        centre.Y.ShouldBe(0, tolerance: 1e-9);
    }

    [Fact]
    public void Contains_FollowsTheShiftedCentre()
    {
        var nodeA = new Vector2D(0, 0);
        var nodeB = new Vector2D(0, 200);

        SensorPicture.Contains(SensorKind.Accelerometer, new Vector2D(0, 106), nodeA, nodeB, _servo, _hub).ShouldBeTrue();
        SensorPicture.Contains(SensorKind.Accelerometer, new Vector2D(0, 93), nodeA, nodeB, _servo, _hub).ShouldBeFalse();
    }

    [Fact]
    public void SizeOf_Camera_IsAboutTwiceTheAccelerometer()
    {
        SensorPicture.SizeOf(SensorKind.Camera).ShouldBeGreaterThan(1.8 * SensorPicture.SizeOf(SensorKind.Accelerometer));
    }
}
