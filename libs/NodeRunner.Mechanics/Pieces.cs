using NodeRunner.Domain;

namespace NodeRunner.Mechanics;

/// <summary>The one-piece check: which joints a creature's links join together.</summary>
public static class Pieces
{
    /// <summary>
    /// The pieces <paramref name="links"/> join <paramref name="nodes"/> into, each its node
    /// ids in node order and listed by its first node. Loose joints are in none, as they have their
    /// own problem when building; Servos and sensors join nothing new. A tear asks it
    /// again over the links still attached (<see cref="Tear.MainPiece"/>).
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<int>> Of(IReadOnlyList<NodeDef> nodes, IEnumerable<LinkRef> links)
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
}
