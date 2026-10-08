using Godot;
using NodeRunner.Domain;

namespace NodeRunner.Ui.Widgets;

/// <summary>The sensor move's line (#1021): no tile follows the finger, so a line ties it to the sensor it moves.</summary>
public partial class BuildCanvas
{
    /// <summary>
    /// The drag line from the moving sensor's picture to the finger, or to its picture on a beam that
    /// takes it, with an arrowhead (`docs/WORLD_VISUALS.md` → "Moving a sensor").
    /// </summary>
    private void DrawSensorMoveLine(CanvasItem canvas)
    {
        if (_viewModel is null || _gestures?.MovingSensorId is not { } sensorId || _gestures.SensorDragEnd is not { } finger
            || _viewModel.Sensors.SingleOrDefault(sensor => sensor.Id == sensorId) is not { } moving)
        {
            return;
        }

        var preview = _gestures.MovedSensorPreview;
        var refused = _gestures.SensorDropTarget is { } target && !_viewModel.CanMoveSensor(sensorId, target, out _);
        var from = BeamMiddle(moving.BeamId);
        var to = preview is null ? ToGodot(finger) : BeamMiddle(preview.BeamId);
        // Like a link drag's line, it starts at the sensor's picture, and ends at its preview or the finger.
        var pictureRadius = (float)SensorPicture.SizeOf(moving.Kind) / 2;
        var endRadius = preview is null ? 0 : pictureRadius;
        if (from.DistanceTo(to) <= pictureRadius + endRadius + (refused ? _refusedMarkRadius * 2 : 0))
        {
            return;
        }

        using var pen = ViewPen(canvas);
        var state = refused ? DragLine.Refused : preview is null ? DragLine.Free : DragLine.Attaches;
        DrawDragLine(pen, from.MoveToward(to, pictureRadius), to.MoveToward(from, endRadius), state, arrow: true);
    }

    private Vector2 BeamMiddle(int beamId)
    {
        var beam = _viewModel!.Beams[_viewModel.BeamIndexOf(beamId)];
        return (ToGodot(NodeById(beam.NodeA).Position) + ToGodot(NodeById(beam.NodeB).Position)) / 2;
    }
}
