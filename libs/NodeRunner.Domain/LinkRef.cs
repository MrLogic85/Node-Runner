namespace NodeRunner.Domain;

/// <summary>A beam, Piston or Spring by id, its two end nodes and its kind.</summary>
public readonly record struct LinkRef(int Id, int NodeA, int NodeB, CreatureElementKind Kind)
{
    /// <summary>Every beam, Piston and Spring as a link, in that order; the one place a link kind becomes a <see cref="LinkRef"/>.</summary>
    public static IEnumerable<LinkRef> All(IEnumerable<BeamDef> beams, IEnumerable<PistonDef> pistons, IEnumerable<SpringDef> springs) =>
        beams.Select(beam => new LinkRef(beam.Id, beam.NodeA, beam.NodeB, CreatureElementKind.Beam))
            .Concat(pistons.Select(piston => new LinkRef(piston.Id, piston.NodeA, piston.NodeB, CreatureElementKind.Piston)))
            .Concat(springs.Select(spring => new LinkRef(spring.Id, spring.NodeA, spring.NodeB, CreatureElementKind.Spring)));

    /// <summary>The link with <paramref name="linkId"/> among <paramref name="links"/>.</summary>
    public static LinkRef Find(IEnumerable<LinkRef> links, int linkId)
    {
        foreach (var link in links)
        {
            if (link.Id == linkId)
            {
                return link;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(linkId), "Link id must point to an existing beam, piston or spring.");
    }

    public bool Touches(int nodeId) => NodeA == nodeId || NodeB == nodeId;

    public int FarNodeFrom(int nodeId) => NodeA == nodeId ? NodeB : NodeB == nodeId ? NodeA : throw new ArgumentException("The link does not touch that node.", nameof(nodeId));
}
