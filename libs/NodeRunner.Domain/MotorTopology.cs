namespace NodeRunner.Domain;

/// <summary>
/// Derives how a creature's beams connect and rotate relative to each other,
/// purely from its <see cref="CreatureDef"/> topology. This is structural
/// derivation, not stored data — see docs/CREATURE_MODEL.md for the model it
/// encodes.
///
/// Deliberate exception to this project's "no behavior beyond data
/// validation" rule for `libs/NodeRunner.Domain/` (see
/// `libs/NodeRunner.Domain/AGENTS.md`): both the physics layer
/// (`project/src/creature/`) and any future tooling need the exact same
/// answer, and Domain is the only layer both can depend on without violating
/// the layer graph in docs/ARCHITECTURE.md. This class is pure, stateless,
/// and takes/returns only Domain types.
/// </summary>
public static class MotorTopology
{
    /// <summary>
    /// Builds every physical pin between beams that share a node, plus which
    /// of those pins are genuinely independent motor relations: a node with
    /// N beams grouped into K rigid clusters (clusters of size 1 unless a
    /// triangle locks two or more beams together) contributes N-1 physical
    /// pins but only K-1 motorized ones — beams inside the same cluster
    /// share a single physical degree of freedom, so only one connection
    /// per cluster may carry a sensor/brain output. A node with zero beams
    /// is rejected by <see cref="CreatureDef"/> already; a node with exactly
    /// one beam contributes no pins (a static, passive end).
    /// </summary>
    public static IReadOnlyList<NodeConnectionDef> BuildNodeConnections(CreatureDef creature)
    {
        ArgumentNullException.ThrowIfNull(creature);

        var incidentBeams = BuildIncidentBeams(creature);
        var lockedPairs = FindRigidTrianglePairs(creature);

        var connections = new List<NodeConnectionDef>();
        for (var nodeIndex = 0; nodeIndex < creature.Nodes.Count; nodeIndex++)
        {
            var beams = incidentBeams[nodeIndex];
            if (beams.Count < 2)
            {
                continue;
            }

            connections.AddRange(BuildNodeConnectionsForNode(nodeIndex, beams, lockedPairs[nodeIndex]));
        }

        return connections;
    }

    // Beams locked pairwise by a triangle can be locked transitively too (a
    // beam shared by two overlapping triangles, for example), so the pins at
    // a node must be grouped into rigid clusters, not just paired up. Every
    // beam in the same cluster shares one true rotational degree of freedom
    // relative to the rest of the node; only one connection per *cluster*
    // (beyond the node's own reference cluster) is motorized. Beams inside a
    // cluster still need physical pins to hold them at the shared point, so
    // they get non-motorized connections chained to their cluster's leader.
    private static List<NodeConnectionDef> BuildNodeConnectionsForNode(
        int nodeIndex, List<int> beams, HashSet<(int, int)> lockedPairsAtNode)
    {
        var leaderOf = GroupIntoRigidClusters(beams, lockedPairsAtNode);

        // Deterministic: the beam with the lowest index is always some
        // cluster's leader, so using it as the node's overall reference is
        // well-defined regardless of cluster contents or ordering.
        var reference = beams.Min();
        var referenceLeader = leaderOf[reference];

        var connections = new List<NodeConnectionDef>();
        foreach (var beam in beams)
        {
            if (beam == reference)
            {
                continue;
            }

            var leader = leaderOf[beam];
            if (leader == referenceLeader)
            {
                // Same rigid cluster as the reference: physically pinned,
                // but not an independent motor relation.
                connections.Add(new NodeConnectionDef(nodeIndex, reference, beam, IsMotorized: false));
            }
            else if (beam == leader)
            {
                // First (and only) representative of a distinct cluster:
                // this is the one genuine motor relation for that cluster.
                connections.Add(new NodeConnectionDef(nodeIndex, reference, beam, IsMotorized: true));
            }
            else
            {
                // A non-leader member of another cluster: pin it to its own
                // cluster's leader instead of duplicating the leader's
                // motor relation.
                connections.Add(new NodeConnectionDef(nodeIndex, leader, beam, IsMotorized: false));
            }
        }

        return connections;
    }

    // Union-find over this node's incident beams, using the locked-pair
    // edges. Returns, per beam, the lowest-indexed beam in its cluster.
    private static Dictionary<int, int> GroupIntoRigidClusters(List<int> beams, HashSet<(int, int)> lockedPairsAtNode)
    {
        var parent = beams.ToDictionary(beam => beam, beam => beam);

        int Find(int beam)
        {
            while (parent[beam] != beam)
            {
                parent[beam] = parent[parent[beam]];
                beam = parent[beam];
            }

            return beam;
        }

        void Union(int a, int b)
        {
            var rootA = Find(a);
            var rootB = Find(b);
            if (rootA != rootB)
            {
                parent[Math.Max(rootA, rootB)] = Math.Min(rootA, rootB);
            }
        }

        foreach (var (a, b) in lockedPairsAtNode)
        {
            Union(a, b);
        }

        return beams.ToDictionary(beam => beam, Find);
    }

    private static List<int>[] BuildIncidentBeams(CreatureDef creature)
    {
        var incident = new List<int>[creature.Nodes.Count];
        for (var i = 0; i < incident.Length; i++)
        {
            incident[i] = [];
        }

        for (var beamIndex = 0; beamIndex < creature.Beams.Count; beamIndex++)
        {
            var beam = creature.Beams[beamIndex];
            incident[beam.NodeA].Add(beamIndex);
            incident[beam.NodeB].Add(beamIndex);
        }

        foreach (var list in incident)
        {
            list.Sort();
        }

        return incident;
    }

    // A closed triangle of three beams between three nodes is geometrically
    // rigid (SSS): all three side lengths fix all three vertex angles. Find
    // every such triangle and record, per node, which beam pair it locks —
    // the pair of beams meeting at that vertex.
    private static HashSet<(int, int)>[] FindRigidTrianglePairs(CreatureDef creature)
    {
        var locked = new HashSet<(int, int)>[creature.Nodes.Count];
        for (var i = 0; i < locked.Length; i++)
        {
            locked[i] = [];
        }

        var beams = creature.Beams;
        for (var a = 0; a < beams.Count; a++)
        {
            for (var b = a + 1; b < beams.Count; b++)
            {
                if (!TryShareNode(beams[a], beams[b], out var sharedNode, out var farA, out var farB))
                {
                    continue;
                }

                if (!TryFindBeamBetween(creature, farA, farB, out var closingBeam))
                {
                    continue;
                }

                if (closingBeam == a || closingBeam == b)
                {
                    continue;
                }

                locked[sharedNode].Add(PairKey(a, b));
            }
        }

        return locked;
    }

    private static bool TryShareNode(BeamDef beamA, BeamDef beamB, out int sharedNode, out int farA, out int farB)
    {
        (int Shared, int Far)? match = null;

        if (beamA.NodeA == beamB.NodeA)
        {
            match = (beamA.NodeA, 0);
        }
        else if (beamA.NodeA == beamB.NodeB)
        {
            match = (beamA.NodeA, 1);
        }
        else if (beamA.NodeB == beamB.NodeA)
        {
            match = (beamA.NodeB, 2);
        }
        else if (beamA.NodeB == beamB.NodeB)
        {
            match = (beamA.NodeB, 3);
        }

        if (match is null)
        {
            sharedNode = farA = farB = -1;
            return false;
        }

        sharedNode = match.Value.Shared;
        farA = match.Value.Far is 0 or 1 ? beamA.NodeB : beamA.NodeA;
        farB = match.Value.Far switch
        {
            0 => beamB.NodeB,
            1 => beamB.NodeA,
            2 => beamB.NodeB,
            _ => beamB.NodeA,
        };

        return farA != farB;
    }

    private static bool TryFindBeamBetween(CreatureDef creature, int nodeX, int nodeY, out int beamIndex)
    {
        for (var i = 0; i < creature.Beams.Count; i++)
        {
            var beam = creature.Beams[i];
            if ((beam.NodeA == nodeX && beam.NodeB == nodeY) || (beam.NodeA == nodeY && beam.NodeB == nodeX))
            {
                beamIndex = i;
                return true;
            }
        }

        beamIndex = -1;
        return false;
    }

    private static (int, int) PairKey(int a, int b) => (Math.Min(a, b), Math.Max(a, b));
}
