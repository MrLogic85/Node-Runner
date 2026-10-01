namespace NodeRunner.Domain.Tests;

public sealed class SensorPictureTests
{
    [Fact]
    public void Contains_UsesTheSquareAtTheMidpointTurnedWithTheBeam()
    {
        var nodeA = new Vector2D(0, 0);
        var nodeB = new Vector2D(0, 100);
        const double half = SensorPicture.Size / 2;

        SensorPicture.Contains(new Vector2D(half - 1, 50), nodeA, nodeB).ShouldBeTrue();
        SensorPicture.Contains(new Vector2D(half + 1, 50), nodeA, nodeB).ShouldBeFalse();
        SensorPicture.Contains(new Vector2D(0, 50 + half - 1), nodeA, nodeB).ShouldBeTrue();
        SensorPicture.Contains(new Vector2D(0, 50 + half + 1), nodeA, nodeB).ShouldBeFalse();
    }

    [Fact]
    public void Contains_OnAZeroLengthBeam_StillHasAPicture()
    {
        var node = new Vector2D(10, 10);

        SensorPicture.Contains(new Vector2D(12, 12), node, node).ShouldBeTrue();
    }

    [Fact]
    public void Size_FitsInTheFreeLengthABeamMustLeave()
    {
        SensorPicture.Size.ShouldBeLessThan(30);
    }
}
