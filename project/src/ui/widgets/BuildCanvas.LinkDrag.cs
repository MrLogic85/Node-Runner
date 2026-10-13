using Godot;
using NodeRunner.Domain;
using NodeRunner.Theme;

namespace NodeRunner.Ui.Widgets;

/// <summary>The link drag's drawing (#920, #849): the joints it can start from, its line, its end rings and the link it replaces.</summary>
public partial class BuildCanvas
{
    // The Links tool's dashed rings, a refused target's (#451) and a link start's (#1057): this many dashes, each this many segments.
    private const int _ringDashes = 12;
    private const int _ringDashSegments = 6;

    // The picked link's start rings (docs/WORLD_VISUALS.md → "Picked link"). SelectedNodeIds is
    // skipped too, as LinkDrawnFrom still gives a selected Servo's joint a link.
    private void DrawLinkStarts(CanvasItem canvas)
    {
        if (_viewModel is null || _gestures is null || _gestures.BeamStartNodeId is not null)
        {
            return;
        }

        var selected = _viewModel.SelectedNodeIds;
        using var pen = ViewPen(canvas);
        foreach (var node in _viewModel.Nodes)
        {
            if (selected.Contains(node.Id) || _gestures.LinkDrawnFrom(node.Id) is null)
            {
                continue;
            }

            var radius = (float)SelectionMarks.JointHalo(_viewModel.NodeRadius(node.Id));
            pen.DashedRing(ToGodot(node.Position), radius, _ringDashes, _ringDashSegments, Theme.SelectionGlow, Stroke(Theme.SelectionRingWidth));
        }
    }

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
        // Like a beam, it starts at the joint's hub (#1086), and ends at the target's hub or the finger.
        if (JointDrawing.BeamSpan(Theme.JointRingWidth, ToGodot(from.Position), JointDrawing.HubRadius, ToGodot(to), target is null ? 0 : JointDrawing.HubRadius) is not (var lineStart, var lineEnd))
        {
            return;
        }

        using var pen = ViewPen(canvas);
        DrawDragLine(pen, lineStart, lineEnd, refused is not null ? DragLine.Refused : target is not null ? DragLine.Attaches : DragLine.Free, arrow: false);
    }

    // A link dropped now replaces the link of another kind on its pair (#849, #1176): a dashed outline at
    // that link's selection offset, from hub to hub over the creature, says so.
    private void DrawReplacedLink(CanvasItem canvas)
    {
        if (_viewModel is null || _gestures?.ReplacedLinkId is not { } linkId || ReplacedLinkLine(linkId) is not var (nodeA, nodeB, offset))
        {
            return;
        }

        var a = NodeById(nodeA);
        var b = NodeById(nodeB);
        if (JointDrawing.BeamSpan(Theme.JointRingWidth, ToGodot(a.Position), JointDrawing.HubRadius, ToGodot(b.Position), JointDrawing.HubRadius) is not (var start, var end))
        {
            return;
        }

        using var pen = ViewPen(canvas);
        var across = (end - start).Normalized().Orthogonal() * offset;
        var width = Stroke(Theme.SelectedBeamLineWidth);
        pen.DashedLine(start + across, end + across, Theme.Beam, width, _dragLineDash);
        pen.DashedLine(start - across, end - across, Theme.Beam, width, _dragLineDash);
    }

    // The replaced link's joints and its selection outline's distance from its axis.
    private (int NodeA, int NodeB, float Offset)? ReplacedLinkLine(int linkId)
    {
        if (_viewModel!.Beams.FirstOrDefault(beam => beam.Id == linkId) is { } beam)
        {
            return (beam.NodeA, beam.NodeB, Theme.SelectedBeamOffset);
        }

        if (_viewModel.Pistons.FirstOrDefault(piston => piston.Id == linkId) is { } piston)
        {
            return (piston.NodeA, piston.NodeB, PistonDrawing.SelectionOffset(Theme));
        }

        return _viewModel.Springs.FirstOrDefault(spring => spring.Id == linkId) is { } spring
            ? (spring.NodeA, spring.NodeB, SpringDrawing.SelectionOffset(Theme))
            : null;
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
            pen.DashedRing(ToGodot(node.Position), radius, _ringDashes, _ringDashSegments, Theme.Danger, Stroke(Theme.MotorSignalWidth));
        }
    }
}
