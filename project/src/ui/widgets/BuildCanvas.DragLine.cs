using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The line a link drag (#920) or a sensor move (#1021) draws from where it started, since the finger
/// hides its target: one look for both (`docs/WORLD_VISUALS.md` → "Link drag line").
/// </summary>
public partial class BuildCanvas
{
    // The loose-joint style mark a drag line shows at its middle: a cross when refused (#920), else a
    // sensor move's arrow (#1024).
    private const float _lineMarkRadius = 12;

    // How far an arrow mark's tip, tail and head reach from the middle, in units of the cross's arm, so
    // it fills its ring as the cross does.
    private const float _arrowReach = 1.4f;

    // The dash of a free or refused drag line, and of the outline round a beam a link will replace (#849).
    private const float _dragLineDash = 8;

    private enum DragLine
    {
        Free,
        Attaches,
        Refused,
    }

    /// <summary>
    /// Draws <paramref name="state"/>'s look; <paramref name="arrow"/> adds an arrow mark at the middle
    /// of a line that is not refused, pointing to its end, when there is room for it beside its ends.
    /// </summary>
    private void DrawDragLine(UiPixelPen pen, Vector2 start, Vector2 end, DragLine state, bool arrow)
    {
        var width = Stroke(Theme.BeamWidth);
        var middle = (start + end) / 2;
        switch (state)
        {
            case DragLine.Refused:
                pen.DashedLine(start, end, Theme.Danger, width, _dragLineDash);
                DrawLineMark(pen, middle, Theme.Danger, [[new(-1, -1), new(1, 1)], [new(1, -1), new(-1, 1)]]);
                return;
            case DragLine.Attaches:
                pen.Line(start, end, Theme.SelectionGlow, width);
                break;
            default:
                pen.DashedLine(start, end, Theme.SelectionGlow, width, _dragLineDash);
                break;
        }

        if (arrow && start.DistanceTo(end) > _lineMarkRadius * 2)
        {
            var along = (end - start).Normalized();
            var across = along.Orthogonal();
            // An arrow whose right-angled head reaches back to the middle, its shaft on the line so the eye
            // carries the line through the ring.
            var tip = along * _arrowReach;
            var side = across * _arrowReach;
            DrawLineMark(pen, middle, Theme.SelectionGlow, [[-tip, tip], [tip, side], [tip, -side]]);
        }
    }

    /// <summary>
    /// A drag line's mark: <paramref name="strokes"/> on a ring round a clear disc, so it keeps its shape
    /// at any angle of the line. A stroke's points are offsets from the middle in units of the mark's arm.
    /// </summary>
    private void DrawLineMark(UiPixelPen pen, Vector2 middle, Color color, Vector2[][] strokes)
    {
        var width = Stroke(Theme.MotorSignalWidth);
        var arm = _lineMarkRadius * 0.45f;
        pen.Disc(middle, _lineMarkRadius, Theme.ArenaBackground);
        pen.Ring(middle, _lineMarkRadius, color, width);
        pen.Strokes(strokes.Select(stroke => stroke.Select(point => middle + (point * arm)).ToArray()), color, width);
    }
}
