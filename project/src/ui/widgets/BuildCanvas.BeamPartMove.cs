using Godot;
using NodeRunner.Domain;

namespace NodeRunner.Ui.Widgets;

/// <summary>The beam part move's line: no tile follows the finger, so a line ties it to the sensor or Memory cell it moves.</summary>
public partial class BuildCanvas
{
    /// <summary>
    /// The drag line from the moving sensor's or Memory cell's picture to the finger, or to its picture on a beam that
    /// takes it, with an arrowhead (`docs/WORLD_VISUALS.md` → "Build canvas" → Moving a sensor or Memory cell).
    /// </summary>
    private void DrawBeamPartMoveLine(CanvasItem canvas)
    {
        if (_viewModel is null || _gestures?.MovingBeamPartId is not { } partId || _gestures.BeamPartDragEnd is not { } finger
            || _viewModel.FindPart(partId) is not IBeamPartDef moving)
        {
            return;
        }

        var preview = _gestures.MovedBeamPartPreview;
        var refused = _gestures.BeamPartDropTarget is { } target && !_viewModel.CanMoveBeamPart(partId, target, out _);
        var from = ToGodot(_viewModel.SensorCentreOn(moving.BeamId));
        var to = ToGodot(preview is null ? finger : _viewModel.SensorCentreOn(preview.BeamId));
        // Like a link drag's line, it starts at the part's picture, and ends at its preview or the finger.
        var pictureRadius = (float)SensorPicture.SizeOf(moving) / 2;
        var endRadius = preview is null ? 0 : pictureRadius;
        if (from.DistanceTo(to) <= pictureRadius + endRadius + (refused ? _lineMarkRadius * 2 : 0))
        {
            return;
        }

        using var pen = ViewPen(canvas);
        var state = refused ? DragLine.Refused : preview is null ? DragLine.Free : DragLine.Attaches;
        DrawDragLine(pen, from.MoveToward(to, pictureRadius), to.MoveToward(from, endRadius), state, arrow: true);
    }
}
