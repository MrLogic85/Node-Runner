using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

public static class CanvasNoteTargets
{
    /// <summary>
    /// The joints a note on <paramref name="target"/> stands by: a joint itself, a joint part's
    /// joint, and both ends of a link or of a sensor's or Memory cell's beam.
    /// </summary>
    public static IReadOnlyList<int> JointIds(CreatureElementSelection target, BuildViewModel build)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(build);
        return build.FindPart(target.Id) switch
        {
            NodeDef node => [node.Id],
            IJointPartDef part => [part.NodeId],
            IBeamPartDef part => LinkJoints(build.Link(part.BeamId)),
            { } part when PartKinds.IsLink(part) => LinkJoints(build.Link(part.Id)),
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
    }

    private static int[] LinkJoints(LinkRef link) => [link.NodeA, link.NodeB];
}
