using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>Build canvas marks for joint parts: a selected Servo's link bands, and the tray placing feedback of a Servo or Wheel (#129).</summary>
public static class BuildServoDrawing
{
    private const float _servoBeamBandHalfWidth = 9;
    private const float _servoPistonBandHalfWidth = 11;
    private const float _servoSpringBandHalfWidth = 13;
    private const int _refusedRingDashes = 8;
    private const int _refusedDashSegments = 8;

    public static void DrawSelectedBands(CanvasItem canvas, BuildViewModel viewModel, VisualTheme theme, Transform2D viewTransform)
    {
        if (viewModel.SingleSelectedServoId is not { } servoId)
        {
            return;
        }

        var servo = viewModel.Servos[viewModel.ServoIndexOf(servoId)];
        var toPixels = UiPixelSpace.Enter(canvas, viewTransform);
        var scale = UiPixelSpace.ScaleOf(toPixels);
        if (servo.FixedLinkId is { } fixedLink)
        {
            DrawLinkBand(canvas, viewModel, theme, toPixels, scale, fixedLink, fixedMark: true);
        }

        if (servo.TargetLinkId is { } targetLink)
        {
            DrawLinkBand(canvas, viewModel, theme, toPixels, scale, targetLink, fixedMark: false);
        }

        canvas.DrawSetTransformMatrix(viewTransform);
    }

    /// <summary>
    /// While a joint part is dragged or picked (#1016), a joint that takes it shows the <c>halo</c> ring
    /// round the part as placed (<see cref="BuildGestures.PlacingRingRadius"/>), and one that holds a
    /// part a dashed <c>danger</c> ring round its own edge.
    /// </summary>
    public static void DrawPlacingFeedback(CanvasItem canvas, BuildViewModel viewModel, BuildPart? placing, VisualTheme theme, Transform2D viewTransform)
    {
        if (placing is not { } part || !PartTray.IsJointPart(part))
        {
            return;
        }

        var partRadius = BuildGestures.PlacingRingRadius(part);

        using var pen = UiPixelPen.Begin(canvas, viewTransform);
        foreach (var node in viewModel.Nodes)
        {
            var target = new CreatureElementSelection(CreatureElementKind.Node, node.Id);
            var canPlace = viewModel.CanPlacePart(part, target, out _);
            var occupied = viewModel.JointPartAt(node.Id) is not null;
            if (!canPlace && !occupied)
            {
                continue;
            }

            var center = ToGodot(node.Position);
            var radius = (float)SelectionMarks.JointHalo(canPlace ? partRadius : viewModel.NodeRadius(node.Id));
            if (canPlace)
            {
                pen.Ring(center, radius, theme.SelectionGlow, theme.SelectionRingWidth);
            }
            else
            {
                pen.DashedRing(center, radius, _refusedRingDashes, _refusedDashSegments, theme.Danger, theme.SelectionRingWidth);
            }
        }
    }

    private static void DrawLinkBand(CanvasItem canvas, BuildViewModel viewModel, VisualTheme theme, Transform2D toPixels, float scale, int linkId, bool fixedMark)
    {
        var link = viewModel.Link(linkId);
        var a = viewModel.Nodes[viewModel.NodeIndexOf(link.NodeA)];
        var b = viewModel.Nodes[viewModel.NodeIndexOf(link.NodeB)];
        SelectionDrawing.DrawLinkBand(
            canvas,
            toPixels,
            scale,
            ToGodot(a.Position),
            ToGodot(b.Position),
            (float)viewModel.NodeRadius(a.Id),
            (float)viewModel.NodeRadius(b.Id),
            BandHalfWidth(link.Kind),
            fixedMark ? theme.ServoFixedBand : theme.ServoTargetBand,
            fixedMark ? theme.ServoFixedBandEdge : theme.ServoTargetBandEdge,
            fixedMark ? theme.ServoFixedHatch : null,
            theme.RigidHatchSpacing);
    }

    private static float BandHalfWidth(CreatureElementKind kind) => kind switch
    {
        CreatureElementKind.Beam => _servoBeamBandHalfWidth,
        CreatureElementKind.Piston => _servoPistonBandHalfWidth,
        CreatureElementKind.Spring => _servoSpringBandHalfWidth,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a Servo link kind."),
    };

    private static Vector2 ToGodot(Vector2D position) => new((float)position.X, (float)position.Y);
}
