using System.Diagnostics.CodeAnalysis;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The link half of <see cref="BuildViewModel"/>: drawing a Beam, Piston or Spring
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
    /// <see cref="CanConnectLink(BuildLink, int, int, out UiText?)"/>, and the link of another kind it
    /// would replace on its pair. A locked Creation neither gains nor loses a link with
    /// network ports, which would change the network; a Beam and a Spring may replace each other, since a motor
    /// holding the link keeps its id.
    /// </summary>
    public bool CanConnectLink(BuildLink link, int nodeIdA, int nodeIdB, [NotNullWhen(false)] out UiText? reason, out int? replacedLinkId)
    {
        replacedLinkId = null;
        if (_locked && BuildLinkList.HasNetworkPorts(link))
        {
            reason = LockedReason;
            return false;
        }

        var can = link switch
        {
            BuildLink.Beam => _builder.CanAddBeam(nodeIdA, nodeIdB, out reason),
            BuildLink.Piston => _builder.CanAddPiston(nodeIdA, nodeIdB, out reason),
            BuildLink.Spring => _builder.CanAddSpring(nodeIdA, nodeIdB, out reason),
            _ => throw new ArgumentOutOfRangeException(nameof(link), "Only a Beam, a Piston or a Spring is drawn between two joints."),
        };
        if (can)
        {
            replacedLinkId = _builder.LinkBetween(nodeIdA, nodeIdB);
        }

        if (_locked && replacedLinkId is { } replaced && BuildLinkList.HasNetworkPorts(KindOf(replaced)))
        {
            replacedLinkId = null;
            reason = LockedReason;
            return false;
        }

        return can;
    }

    private BuildLink KindOf(int linkId) =>
        _builder.Pistons.Any(piston => piston.Id == linkId) ? BuildLink.Piston
        : _builder.Springs.Any(spring => spring.Id == linkId) ? BuildLink.Spring
        : BuildLink.Beam;

    /// <summary>
    /// Places <paramref name="link"/>, a Beam, a Piston or a Spring, between two nodes and
    /// returns its id; it replaces a link of another kind there. A refused pair changes nothing and shows why as <see cref="PlacementNote"/>
    /// at <paramref name="nodeIdB"/>, the joint the drag ended on.
    /// </summary>
    public int? ConnectLink(BuildLink link, int nodeIdA, int nodeIdB)
    {
        PlacementNote = null;
        if (!CanConnectLink(link, nodeIdA, nodeIdB, out var reason))
        {
            if (nodeIdA != nodeIdB && _builder.Nodes.Any(node => node.Id == nodeIdB))
            {
                PlacementNote = new CanvasNote(CanvasNoteKind.Danger, new CreatureElementSelection(CreatureElementKind.Node, nodeIdB), reason);
            }

            return null;
        }

        var linkId = _history.Change(() =>
        {
            var id = link switch
            {
                BuildLink.Beam => _builder.AddBeam(nodeIdA, nodeIdB),
                BuildLink.Piston => _builder.AddPiston(nodeIdA, nodeIdB),
                _ => _builder.AddSpring(nodeIdA, nodeIdB),
            };

            // Within the step: its Undo row refresh must not find a replaced link still selected.
            PruneSelection();

            return id;
        });
        SelectionChanged();
        return linkId;
    }
}
