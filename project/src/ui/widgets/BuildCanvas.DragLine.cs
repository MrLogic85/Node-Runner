using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The line a link drag (#920) or a sensor move (#1021) draws from where it started, since the finger
/// hides its target: one look for both (`docs/WORLD_VISUALS.md` → "Link drag line").
/// </summary>
public partial class BuildCanvas
{
    // The loose-joint style mark a refused drag line shows at its middle (#920).
    private const float _refusedMarkRadius = 12;

    // The dash of a free or refused drag line, and of the outline round a beam a link will replace (#849).
    private const float _dragLineDash = 8;

    // A sensor move's arrowhead (#1021), in beam widths: a dart twice as long as it is wide, with a
    // notched back, so its tip is its only sharp point and it reads along the line at any angle.
    private const float _arrowLength = 4.5f;
    private const float _arrowWidth = 2.25f;
    private const float _arrowNotch = 0.3f;

    private enum DragLine
    {
        Free,
        Attaches,
        Refused,
    }

    /// <summary>
    /// Draws <paramref name="state"/>'s look; <paramref name="arrow"/> adds an arrowhead at the middle of
    /// a line that is not refused, pointing to its end, when there is room for it beside its ends.
    /// </summary>
    private void DrawDragLine(UiPixelPen pen, Vector2 start, Vector2 end, DragLine state, bool arrow)
    {
        var width = Stroke(Theme.BeamWidth);
        var middle = (start + end) / 2;
        switch (state)
        {
            case DragLine.Refused:
                pen.DashedLine(start, end, Theme.Danger, width, _dragLineDash);
                DrawRefusedMark(pen, middle);
                return;
            case DragLine.Attaches:
                pen.Line(start, end, Theme.SelectionGlow, width);
                break;
            default:
                pen.DashedLine(start, end, Theme.SelectionGlow, width, _dragLineDash);
                break;
        }

        var length = width * _arrowLength;
        if (arrow && start.DistanceTo(end) > length * 3)
        {
            var along = (end - start).Normalized();
            var across = along.Orthogonal() * (width * _arrowWidth / 2);
            var tip = middle + (along * length / 2);
            var back = middle - (along * length / 2);
            var notch = back + (along * length * _arrowNotch);
            pen.Polygon([tip, back + across, notch, back - across], Theme.SelectionGlow);
        }
    }

    /// <summary>The crossed ring at a refused drag line's middle, on a clear disc so the cross keeps its shape at any angle of the line.</summary>
    private void DrawRefusedMark(UiPixelPen pen, Vector2 middle)
    {
        var mark = Stroke(Theme.MotorSignalWidth);
        var arm = _refusedMarkRadius * 0.45f;
        pen.Disc(middle, _refusedMarkRadius, Theme.ArenaBackground);
        pen.Ring(middle, _refusedMarkRadius, Theme.Danger, mark);
        pen.Line(middle + new Vector2(-arm, -arm), middle + new Vector2(arm, arm), Theme.Danger, mark);
        pen.Line(middle + new Vector2(arm, -arm), middle + new Vector2(-arm, arm), Theme.Danger, mark);
    }
}
