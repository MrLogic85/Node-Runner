using System.Diagnostics.CodeAnalysis;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The link half of <see cref="BuildViewModel"/>: drawing a Beam, Piston (#451) or Spring (#453)
/// between two joints, and what refuses it.
/// </summary>
public sealed partial class BuildViewModel
{
    /// <summary>
    /// Whether <see cref="ConnectLink"/> would place <paramref name="link"/> between this pair;
    /// if not, <paramref name="reason"/> says why.
    /// </summary>
    public bool CanConnectLink(BuildLink link, int nodeIdA, int nodeIdB, [NotNullWhen(false)] out UiText? reason) =>
        CanConnectLink(link, nodeIdA, nodeIdB, out reason, out _);

    /// <summary>
    /// <see cref="CanConnectLink(BuildLink, int, int, out UiText?)"/>, and the beam the link would
    /// replace (#849): a Piston or Spring takes the place of a beam on its pair.
    /// </summary>
    public bool CanConnectLink(BuildLink link, int nodeIdA, int nodeIdB, [NotNullWhen(false)] out UiText? reason, out int? replacedBeamId)
    {
        replacedBeamId = null;
        if (_moveOnly)
        {
            reason = MoveOnlyReason;
            return false;
        }

        var can = link switch
        {
            BuildLink.Beam => _builder.CanAddBeam(nodeIdA, nodeIdB, out reason),
            BuildLink.Piston => _builder.CanAddPiston(nodeIdA, nodeIdB, out reason),
            BuildLink.Spring => _builder.CanAddSpring(nodeIdA, nodeIdB, out reason),
            _ => throw new ArgumentOutOfRangeException(nameof(link), "Only a Beam, a Piston or a Spring is drawn between two joints."),
        };
        if (can && link != BuildLink.Beam)
        {
            replacedBeamId = _builder.BeamBetween(nodeIdA, nodeIdB);
        }

        return can;
    }

    /// <summary>
    /// Places <paramref name="link"/>, a Beam, a Piston (#451) or a Spring (#453), between two nodes and
    /// returns its id; a Piston or Spring replaces a beam there (#849). A refused pair changes nothing and shows why as <see cref="PlacementNote"/>
    /// at <paramref name="nodeIdB"/>, the joint the drag ended on.
    /// </summary>
    public int? ConnectLink(BuildLink link, int nodeIdA, int nodeIdB)
    {
        PlacementNote = null;
        if (!CanConnectLink(link, nodeIdA, nodeIdB, out var reason))
        {
            if (!_moveOnly && nodeIdA != nodeIdB && _builder.Nodes.Any(node => node.Id == nodeIdB))
            {
                PlacementNote = new CanvasNote(CanvasNoteKind.Danger, new CreatureElementSelection(CreatureElementKind.Node, nodeIdB), reason);
            }

            return null;
        }

        var linkId = _history.Change(() =>
        {
            var servoJoints = SelectedServoJoints();
            var id = link switch
            {
                BuildLink.Beam => _builder.AddBeam(nodeIdA, nodeIdB),
                BuildLink.Piston => _builder.AddPiston(nodeIdA, nodeIdB),
                _ => _builder.AddSpring(nodeIdA, nodeIdB),
            };

            // Within the step, like a Servo link change: its Undo row refresh must not find a replaced
            // beam (#849), or the old id of a Servo that held it, still selected.
            PruneSelection(servoJoints);

            return id;
        });
        SelectionChanged();
        return linkId;
    }
}
