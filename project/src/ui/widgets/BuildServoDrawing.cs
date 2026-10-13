using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// Build canvas marks for joint parts: the link bands of a selected Servo, Velocity motor (#454),
/// Brake (#1041) or Angle sensor (#1205), a Wheel side's under its tyre (#1118). The bands name the sides,
/// so every hinge part's are drawn alike.
/// </summary>
public static class BuildServoDrawing
{
    private const float _servoBeamBandHalfWidth = 9;
    private const float _servoPistonBandHalfWidth = 11;
    private const float _servoSpringBandHalfWidth = 13;

    public static void DrawSelectedBands(CanvasItem canvas, BuildViewModel viewModel, VisualTheme theme, Transform2D viewTransform)
    {
        IHingePartDef? part = viewModel.SingleSelectedServoId is { } servoId ? viewModel.Servos[viewModel.ServoIndexOf(servoId)]
            : viewModel.SingleSelectedVelocityMotorId is { } motorId ? viewModel.VelocityMotors[viewModel.VelocityMotorIndexOf(motorId)]
            : viewModel.SingleSelectedBrakeId is { } brakeId ? viewModel.Brakes[viewModel.BrakeIndexOf(brakeId)]
            : viewModel.SingleSelectedAngleSensorId is { } angleSensorId ? viewModel.AngleSensors[viewModel.AngleSensorIndexOf(angleSensorId)]
            : null;
        if (part is null)
        {
            return;
        }

        using var pen = UiPixelPen.Begin(canvas, viewTransform);
        if (part.FixedLinkId is { } fixedLink)
        {
            DrawLinkBand(pen, viewModel, theme, fixedLink, fixedMark: true);
        }

        if (part.TargetLinkId is { } targetLink)
        {
            DrawLinkBand(pen, viewModel, theme, targetLink, fixedMark: false);
        }
    }

    // The Wheel, a one-sided link (#1118), gets its band as a ring under its tyre, showing past
    // both edges as a beam's band does past the beam; its see-through face shows the inner edge.
    // The ring is not hatched: its colour alone tells Fixed from Target.
    private static void DrawLinkBand(UiPixelPen pen, BuildViewModel viewModel, VisualTheme theme, int linkId, bool fixedMark)
    {
        var fill = fixedMark ? theme.ServoFixedBand : theme.ServoTargetBand;
        var edge = fixedMark ? theme.ServoFixedBandEdge : theme.ServoTargetBandEdge;
        var hatch = fixedMark ? theme.ServoFixedHatch : (Color?)null;
        if (viewModel.Wheels.FirstOrDefault(wheel => wheel.Id == linkId) is { } wheel)
        {
            var hub = ToGodot(viewModel.Nodes[viewModel.NodeIndexOf(wheel.NodeId)].Position);
            var tyreMiddle = (float)wheel.Radius - (WheelPart.TyreWidth / 2);
            SelectionDrawing.DrawRingBand(
                pen,
                hub,
                tyreMiddle - _servoBeamBandHalfWidth,
                tyreMiddle + _servoBeamBandHalfWidth,
                fill,
                edge);
            return;
        }

        var link = viewModel.Link(linkId);
        SelectionDrawing.DrawLinkBand(
            pen,
            ToGodot(viewModel.Nodes[viewModel.NodeIndexOf(link.NodeA)].Position),
            ToGodot(viewModel.Nodes[viewModel.NodeIndexOf(link.NodeB)].Position),
            BandHalfWidth(link.Kind),
            fill,
            edge,
            hatch,
            theme.RigidHatchSpacing);
    }

    private static float BandHalfWidth(CreatureElementKind kind) => kind switch
    {
        CreatureElementKind.Beam => _servoBeamBandHalfWidth,
        CreatureElementKind.Piston => _servoPistonBandHalfWidth,
        CreatureElementKind.Spring => _servoSpringBandHalfWidth,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a hinge-part link kind."),
    };

    private static Vector2 ToGodot(Vector2D position) => new((float)position.X, (float)position.Y);
}
