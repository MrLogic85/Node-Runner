using Godot;
using NodeRunner.Domain;
using NodeRunner.Theme;

namespace NodeRunner.Ui.Widgets;

/// <summary>The link drag's drawing (#920, #849): its line, its end rings and the beam it replaces.</summary>
public partial class BuildCanvas
{
    // A refused Piston target's ring is dashed (#451): this many dashes, each this many segments.
    private const int _refusedRingDashes = 12;
    private const int _refusedDashSegments = 6;

    private void DrawBeamPreview(CanvasItem canvas)
    {
        if (_viewModel is null || _gestures?.BeamStartNodeId is not { } start || _gestures.BeamEnd is not { } end)
        {
            return;
        }

        // The line itself shows the state, since the finger hides the target (#920, #451, #877). It
        // goes over the creature, since a refused link lies on the link already there.
        var refused = _gestures.RefusedTargetNodeId;
        var from = NodeById(start);
        var target = (_gestures.BeamTargetNodeId ?? refused) is { } id ? NodeById(id) : null;
        var to = target?.Position ?? end;
        // Like a beam, it starts at the joint's ring, and ends at the target's ring or the finger.
        if (JointDrawing.BeamSpan(Theme.JointRingWidth, ToGodot(from.Position), (float)_viewModel.NodeRadius(from.Id), ToGodot(to), (float)(target is null ? 0 : _viewModel.NodeRadius(target.Id))) is not (var lineStart, var lineEnd))
        {
            return;
        }

        using var pen = ViewPen(canvas);
        DrawDragLine(pen, lineStart, lineEnd, refused is not null ? DragLine.Refused : target is not null ? DragLine.Attaches : DragLine.Free, arrow: false);
    }

    // A Piston or Spring dropped now replaces the beam on its pair (#849): a dashed outline at the
    // selection's offset says so. It goes under the creature like a selected beam's lines, so a
    // Servo's clamp, which holds the new link too, stays on top.
    private void DrawReplacedBeam(CanvasItem canvas)
    {
        if (_viewModel is null || _gestures?.ReplacedBeamId is not { } beamId)
        {
            return;
        }

        var beam = _viewModel.Beams[_viewModel.BeamIndexOf(beamId)];
        var a = NodeById(beam.NodeA);
        var b = NodeById(beam.NodeB);
        if (JointDrawing.BeamSpan(Theme.JointRingWidth, ToGodot(a.Position), (float)_viewModel.NodeRadius(a.Id), ToGodot(b.Position), (float)_viewModel.NodeRadius(b.Id)) is not (var start, var end))
        {
            return;
        }

        using var pen = ViewPen(canvas);
        var across = (end - start).Normalized().Orthogonal() * Theme.SelectedBeamOffset;
        var width = Stroke(Theme.SelectedBeamLineWidth);
        pen.DashedLine(start + across, end + across, Theme.Beam, width, _dragLineDash);
        pen.DashedLine(start - across, end - across, Theme.Beam, width, _dragLineDash);
    }

    private void DrawBeamEndRings(CanvasItem canvas)
    {
        if (_viewModel is null || _gestures is null)
        {
            return;
        }

        using var pen = ViewPen(canvas);
        foreach (var nodeId in new[] { _gestures.BeamStartNodeId, _gestures.BeamTargetNodeId })
        {
            if (nodeId is { } id)
            {
                var node = NodeById(id);
                pen.Ring(ToGodot(node.Position), (float)SelectionMarks.JointHalo(_viewModel.NodeRadius(node.Id)), Theme.SelectionGlow, Stroke(Theme.MotorSignalWidth));
            }
        }

        if (_gestures.RefusedTargetNodeId is { } refused)
        {
            var node = NodeById(refused);
            var radius = (float)SelectionMarks.JointHalo(_viewModel.NodeRadius(node.Id));
            for (var dash = 0; dash < _refusedRingDashes; dash++)
            {
                var from = dash * Mathf.Tau / _refusedRingDashes;
                pen.Arc(ToGodot(node.Position), radius, from, from + (Mathf.Tau / _refusedRingDashes / 2), _refusedDashSegments, Theme.Danger, Stroke(Theme.MotorSignalWidth));
            }
        }
    }
}
