using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public class CreatureThumbnailFitTests
{
    private const double _width = 240;
    private const double _height = 96;
    private const double _inset = 12;

    [Fact]
    public void EmptyCreature_HasNoFit() =>
        CreatureThumbnailFit.Of(new CreatureDef([], [], []), _width, _height, _inset).ShouldBeNull();

    [Fact]
    public void ThumbnailWithoutRoom_HasNoFit() =>
        CreatureThumbnailFit.Of(Line(0, 100), _width, 2 * _inset, _inset).ShouldBeNull();

    [Fact]
    public void LoneJoint_IsCentred_AndNotBlownUp()
    {
        var fit = CreatureThumbnailFit.Of(new CreatureDef([new NodeDef(1, new Vector2D(40, -30))], [], []), _width, _height, _inset)!.Value;

        fit.Scale.ShouldBe(CreatureThumbnailFit.LargestScale);
        Place(fit, new Vector2D(40, -30)).ShouldBe(new Vector2D(_width / 2, _height / 2));
    }

    [Theory]
    [InlineData(3000, 0)]
    [InlineData(0, 3000)]
    [InlineData(800, 800)]
    public void WideTallOrSquareCreature_FitsInsideTheInset(double dx, double dy)
    {
        var creature = Line(dx, dy);
        var fit = CreatureThumbnailFit.Of(creature, _width, _height, _inset)!.Value;

        foreach (var node in creature.Nodes)
        {
            ShouldBeInside(fit, node.Position, creature.NodeRadius(node.Id));
        }

        ShouldFillAndCentre(fit, CreatureThumbnailFit.Bounds(creature)!.Value);
    }

    [Fact]
    public void SensorPicture_ReachingPastTheJoints_IsNotClipped()
    {
        // A flat beam: the camera picture at its middle stands taller than the joint rings.
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(600, 0))],
            [new BeamDef(10, 1, 2)],
            [new SensorDef(20, 10, SensorKind.Camera)]);
        var fit = CreatureThumbnailFit.Of(creature, _width, _height, _inset)!.Value;

        ShouldBeInside(fit, new Vector2D(300, 0), SensorPicture.CameraSize * Math.Sqrt(2) / 2);
        var bounds = CreatureThumbnailFit.Bounds(creature)!.Value;
        bounds.Height.ShouldBeGreaterThanOrEqualTo(SensorPicture.CameraSize);
        ShouldFillAndCentre(fit, bounds);
    }

    private static CreatureDef Line(double dx, double dy) => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(dx, dy))],
        [new BeamDef(10, 1, 2)],
        []);

    private static Vector2D Place(CreatureThumbnailFit fit, Vector2D point) =>
        new((point.X * fit.Scale) + fit.Offset.X, (point.Y * fit.Scale) + fit.Offset.Y);

    // The picture reaches the inset on the axis that limits it, and is centred on both.
    private static void ShouldFillAndCentre(CreatureThumbnailFit fit, CanvasRect bounds)
    {
        var min = Place(fit, bounds.Min);
        var max = Place(fit, bounds.Max);
        Math.Max((max.X - min.X) / (_width - (2 * _inset)), (max.Y - min.Y) / (_height - (2 * _inset))).ShouldBe(1, 1e-9);
        ((min.X + max.X) / 2).ShouldBe(_width / 2, 1e-9);
        ((min.Y + max.Y) / 2).ShouldBe(_height / 2, 1e-9);
    }

    private static void ShouldBeInside(CreatureThumbnailFit fit, Vector2D centre, double reach)
    {
        var at = Place(fit, centre);
        var r = reach * fit.Scale;
        (at.X - r).ShouldBeGreaterThanOrEqualTo(_inset - 1e-9);
        (at.Y - r).ShouldBeGreaterThanOrEqualTo(_inset - 1e-9);
        (at.X + r).ShouldBeLessThanOrEqualTo(_width - _inset + 1e-9);
        (at.Y + r).ShouldBeLessThanOrEqualTo(_height - _inset + 1e-9);
    }
}
