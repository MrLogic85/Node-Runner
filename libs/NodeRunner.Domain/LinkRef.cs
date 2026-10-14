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

    /// <summary>
    /// The pieces <paramref name="links"/> join <paramref name="nodes"/> into (#930), each its node
    /// ids in node order and listed by its first node. Loose joints are in none, as they have their
    /// own problem when building; Servos and sensors join nothing new. A tear (#962) asks it
    /// again over the links still attached.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<int>> Pieces(IReadOnlyList<NodeDef> nodes, IEnumerable<LinkRef> links)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(links);
        var neighbours = nodes.ToDictionary(node => node.Id, _ => new List<int>());
        foreach (var link in links)
        {
            neighbours[link.NodeA].Add(link.NodeB);
            neighbours[link.NodeB].Add(link.NodeA);
        }

        var pieceOf = new Dictionary<int, int>();
        var pieceCount = 0;
        foreach (var node in nodes)
        {
            if (neighbours[node.Id].Count == 0 || pieceOf.ContainsKey(node.Id))
            {
                continue;
            }

            pieceOf[node.Id] = pieceCount;
            var pending = new Stack<int>([node.Id]);
            while (pending.TryPop(out var id))
            {
                foreach (var next in neighbours[id])
                {
                    if (pieceOf.TryAdd(next, pieceCount))
                    {
                        pending.Push(next);
                    }
                }
            }

            pieceCount++;
        }

        var pieces = Enumerable.Range(0, pieceCount).Select(_ => new List<int>()).ToList();
        foreach (var node in nodes.Where(node => pieceOf.ContainsKey(node.Id)))
        {
            pieces[pieceOf[node.Id]].Add(node.Id);
        }

        return pieces;
    }

    public bool Touches(int nodeId) => NodeA == nodeId || NodeB == nodeId;

    public int FarNodeFrom(int nodeId) => NodeA == nodeId ? NodeB : NodeB == nodeId ? NodeA : throw new ArgumentException("The link does not touch that node.", nameof(nodeId));
}
