namespace NodeRunner.Domain;

/// <summary>
/// Finds the closed triangles of beams in a creature. Three beams between three nodes fix all three
/// angles (SSS), so a triangle cannot fold; every other joint turns freely (#450). Build hatches
/// these triangles so the player sees which areas are rigid. See docs/CREATURE_MODEL.md.
///
/// Deliberate exception to this project's "no behavior beyond data validation" rule for
/// `libs/NodeRunner.Domain/` (see `libs/NodeRunner.Domain/AGENTS.md`): pure, stateless, and takes
/// and returns only Domain types.
/// </summary>
public static class RigidTriangles
{
    /// <summary>Every closed triangle of beams, by node index, sorted and without duplicates.</summary>
    public static IReadOnlyList<RigidTriangleDef> Of(CreatureDef creature)
    {
        ArgumentNullException.ThrowIfNull(creature);

        var linked = new HashSet<(int, int)>();
        var neighbours = new SortedSet<int>[creature.Nodes.Count];
        for (var i = 0; i < neighbours.Length; i++)
        {
            neighbours[i] = [];
        }

        foreach (var beam in creature.Beams)
        {
            var a = creature.NodeIndexOf(beam.NodeA);
            var b = creature.NodeIndexOf(beam.NodeB);
            linked.Add((Math.Min(a, b), Math.Max(a, b)));
            neighbours[a].Add(b);
            neighbours[b].Add(a);
        }

        var triangles = new List<RigidTriangleDef>();
        for (var a = 0; a < neighbours.Length; a++)
        {
            foreach (var b in neighbours[a].Where(b => b > a))
            {
                foreach (var c in neighbours[b].Where(c => c > b && linked.Contains((a, c))))
                {
                    triangles.Add(new RigidTriangleDef(a, b, c));
                }
            }
        }

        return triangles;
    }
}
