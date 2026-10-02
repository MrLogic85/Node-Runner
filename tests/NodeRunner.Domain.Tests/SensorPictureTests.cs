namespace NodeRunner.Domain.Tests;

public sealed class SensorPictureTests
{
    [Theory]
    [InlineData(SensorKind.Accelerometer)]
    [InlineData(SensorKind.Camera)]
    public void Contains_UsesTheKindsSquareAtTheMidpointTurnedWithTheBeam(SensorKind kind)
    {
        var nodeA = new Vector2D(0, 0);
        var nodeB = new Vector2D(0, 100);
        var half = SensorPicture.SizeOf(kind) / 2;

        SensorPicture.Contains(kind, new Vector2D(half - 1, 50), nodeA, nodeB).ShouldBeTrue();
        SensorPicture.Contains(kind, new Vector2D(half + 1, 50), nodeA, nodeB).ShouldBeFalse();
        SensorPicture.Contains(kind, new Vector2D(0, 50 + half - 1), nodeA, nodeB).ShouldBeTrue();
        SensorPicture.Contains(kind, new Vector2D(0, 50 + half + 1), nodeA, nodeB).ShouldBeFalse();
    }

    [Fact]
    public void Contains_OnAZeroLengthBeam_StillHasAPicture()
    {
        var node = new Vector2D(10, 10);

        SensorPicture.Contains(SensorKind.Accelerometer, new Vector2D(12, 12), node, node).ShouldBeTrue();
    }

    [Fact]
    public void SizeOf_Camera_IsAboutTwiceTheAccelerometer()
    {
        SensorPicture.SizeOf(SensorKind.Camera).ShouldBeGreaterThan(1.8 * SensorPicture.SizeOf(SensorKind.Accelerometer));
    }

    [Fact]
    public void LargestSize_CoversEveryKind()
    {
        foreach (var kind in Enum.GetValues<SensorKind>())
        {
            SensorPicture.SizeOf(kind).ShouldBeLessThanOrEqualTo(SensorPicture.LargestSize);
        }
    }
}
