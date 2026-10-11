using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>Build canvas marks for joint parts: a selected Servo's link bands, a Wheel side's as a spoke (#1118).</summary>
public static class BuildServoDrawing
{
    private const float _servoBeamBandHalfWidth = 9;
    private const float _servoPistonBandHalfWidth = 11;
    private const float _servoSpringBandHalfWidth = 13;

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

    // The Wheel, a one-sided link (#1118), gets its band as a spoke from its joint to its tyre along
    // its turn, which is 0 in Build.
    private static void DrawLinkBand(CanvasItem canvas, BuildViewModel viewModel, VisualTheme theme, Transform2D toPixels, float scale, int linkId, bool fixedMark)
    {
        Vector2 from;
        Vector2 to;
        float halfWidth;
        if (viewModel.Wheels.FirstOrDefault(wheel => wheel.Id == linkId) is { } wheel)
        {
            from = ToGodot(viewModel.Nodes[viewModel.NodeIndexOf(wheel.NodeId)].Position);
            to = from + new Vector2((float)wheel.Radius, 0);
            halfWidth = _servoBeamBandHalfWidth;
        }
        else
        {
            var link = viewModel.Link(linkId);
            from = ToGodot(viewModel.Nodes[viewModel.NodeIndexOf(link.NodeA)].Position);
            to = ToGodot(viewModel.Nodes[viewModel.NodeIndexOf(link.NodeB)].Position);
            halfWidth = BandHalfWidth(link.Kind);
        }

        SelectionDrawing.DrawLinkBand(
            canvas,
            toPixels,
            scale,
            from,
            to,
            halfWidth,
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
