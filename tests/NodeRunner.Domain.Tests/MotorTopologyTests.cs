namespace NodeRunner.Domain.Tests;

public sealed class MotorTopologyTests
{
    [Fact]
    public void BuildRigidTriangles_ClosedTriangle_ReturnsOneSortedTriangle()
    {
        var creature = new CreatureDef(
            new[]
            {
                new NodeDef(1, new Vector2D(0, 0), 1),
                new NodeDef(2, new Vector2D(1, 0), 1),
                new NodeDef(3, new Vector2D(0, 1), 1),
            },
            new[] { new BeamDef(101, 2, 3), new BeamDef(102, 3, 1), new BeamDef(103, 1, 2) },
            []);

        var triangles = MotorTopology.BuildRigidTriangles(creature);

        triangles.ShouldBe([new RigidTriangleDef(0, 1, 2)]);
    }

    [Fact]
    public void BuildRigidTriangles_Chain_ReturnsNoTriangles()
    {
        var creature = new CreatureDef(
            new[]
            {
                new NodeDef(1, new Vector2D(0, 0), 1),
                new NodeDef(2, new Vector2D(1, 0), 1),
                new NodeDef(3, new Vector2D(2, 0), 1),
            },
            new[] { new BeamDef(101, 1, 2), new BeamDef(102, 2, 3) },
            []);

        var triangles = MotorTopology.BuildRigidTriangles(creature);

        triangles.ShouldBeEmpty();
    }

    [Fact]
    public void BuildNodeConnections_EndNodeWithOneBeam_HasNoConnections()
    {
        // Two nodes, one beam: both ends have degree 1, i.e. no free rotation.
        var creature = new CreatureDef(
            new[]
            {
                new NodeDef(1, new Vector2D(0, 0), 1),
                new NodeDef(2, new Vector2D(1, 0), 1),
            },
            new[] { new BeamDef(101, 1, 2) },
            []);

        var connections = MotorTopology.BuildNodeConnections(creature);

        connections.ShouldBeEmpty();
    }

    [Fact]
    public void BuildNodeConnections_ThreeNodeChain_HasOneMotorizedConnectionAtMiddleNode()
    {
        // Node1 sits between two beams: one motorized connection there.
        var creature = new CreatureDef(
            new[]
            {
                new NodeDef(1, new Vector2D(0, 0), 1),
                new NodeDef(2, new Vector2D(1, 0), 1),
                new NodeDef(3, new Vector2D(2, 0), 1),
            },
            new[] { new BeamDef(101, 1, 2), new BeamDef(102, 2, 3) },
            []);

        var connections = MotorTopology.BuildNodeConnections(creature);

        connections.Count.ShouldBe(1);
        connections[0].NodeIndex.ShouldBe(1);
        connections[0].ReferenceBeamIndex.ShouldBe(0);
        connections[0].OtherBeamIndex.ShouldBe(1);
        connections[0].IsMotorized.ShouldBeTrue();
    }

    [Fact]
    public void BuildNodeConnections_Worm_KeepsMotorRelationOrderFromBeamList()
    {
        var creature = new CreatureDef(
            [
                new NodeDef(1, new Vector2D(0, 0), 1),
                new NodeDef(2, new Vector2D(1, 0), 1),
                new NodeDef(3, new Vector2D(2, 0), 1),
                new NodeDef(4, new Vector2D(3, 0), 1),
                new NodeDef(5, new Vector2D(4, 0), 1),
            ],
            [
                new BeamDef(6, 1, 2),
                new BeamDef(7, 2, 3),
                new BeamDef(8, 3, 4),
                new BeamDef(9, 4, 5),
            ],
            [new SensorDef(10, 6, SensorKind.Accelerometer)]);

        var motorized = MotorTopology.BuildNodeConnections(creature)
            .Where(connection => connection.IsMotorized)
            .ToArray();

        motorized.Select(connection => connection.NodeIndex).ShouldBe([1, 2, 3]);
        motorized.Select(connection => connection.ReferenceBeamIndex).ShouldBe([0, 1, 2]);
        motorized.Select(connection => connection.OtherBeamIndex).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void BuildNodeConnections_ClosedTriangle_HasNoMotorizedConnections()
    {
        // Three nodes, three beams closing a triangle: every vertex angle is
        // locked by the fixed side lengths (SSS), so no relation is free.
        var creature = new CreatureDef(
            new[]
            {
                new NodeDef(1, new Vector2D(0, 0), 1),
                new NodeDef(2, new Vector2D(1, 0), 1),
                new NodeDef(3, new Vector2D(0, 1), 1),
            },
            new[] { new BeamDef(101, 1, 2), new BeamDef(102, 2, 3), new BeamDef(103, 3, 1) },
            []);

        var connections = MotorTopology.BuildNodeConnections(creature);

        connections.ShouldAllBe(connection => !connection.IsMotorized);
    }

    [Fact]
    public void BuildNodeConnections_TriangleWithExtraBeam_ExtraBeamGetsExactlyOneMotorRelation()
    {
        // A rigid triangle (0,1,2) with an extra beam from node 0 to node 3.
        // The triangle's own two beams at node 0 share one rigid cluster, so
        // the extra beam's rotation relative to that cluster is exactly one
        // motor relation — not one per triangle beam, since both triangle
        // beams move together.
        var creature = new CreatureDef(
            new[]
            {
                new NodeDef(1, new Vector2D(0, 0), 1),
                new NodeDef(2, new Vector2D(1, 0), 1),
                new NodeDef(3, new Vector2D(0, 1), 1),
                new NodeDef(4, new Vector2D(-1, 0), 1),
            },
            new[]
            {
                new BeamDef(101, 1, 2),
                new BeamDef(102, 2, 3),
                new BeamDef(103, 3, 1),
                new BeamDef(104, 1, 4),
            },
            []);

        var connections = MotorTopology.BuildNodeConnections(creature);

        var atNodeZero = connections.Where(connection => connection.NodeIndex == 0).ToArray();
        atNodeZero.Length.ShouldBe(2);
        atNodeZero.Count(connection => connection.IsMotorized).ShouldBe(1);

        // Beam 0 (0-1) is node 0's overall reference (lowest index); beam 2
        // (2-0) is in the same rigid cluster as beam 0, so it's pinned but
        // not motorized; beam 3 (the extra beam) is the sole motorized
        // relation for its own, distinct cluster.
        atNodeZero.ShouldContain(connection =>
            connection.ReferenceBeamIndex == 0 && connection.OtherBeamIndex == 2 && !connection.IsMotorized);
        atNodeZero.ShouldContain(connection =>
            connection.ReferenceBeamIndex == 0 && connection.OtherBeamIndex == 3 && connection.IsMotorized);
    }

    [Fact]
    public void BuildNodeConnections_TwoTrianglesSharingADiagonal_AreFullyRigidEvenWithoutADirectLockedPair()
    {
        // A quad split into two triangles by a diagonal (0-2) is fully rigid,
        // but beams 0-1 and 3-0 are only *transitively* locked at node 0 (both
        // lock against the diagonal, not against each other directly) — this
        // exercises clustering beyond simple pairwise triangle detection.
        var creature = new CreatureDef(
            new[]
            {
                new NodeDef(1, new Vector2D(0, 0), 1),
                new NodeDef(2, new Vector2D(1, 0), 1),
                new NodeDef(3, new Vector2D(1, 1), 1),
                new NodeDef(4, new Vector2D(0, 1), 1),
            },
            new[]
            {
                new BeamDef(101, 1, 2),
                new BeamDef(102, 2, 3),
                new BeamDef(103, 3, 4),
                new BeamDef(104, 4, 1),
                new BeamDef(105, 1, 3),
            },
            []);

        var connections = MotorTopology.BuildNodeConnections(creature);

        connections.ShouldAllBe(connection => !connection.IsMotorized);

        var atNodeZero = connections.Where(connection => connection.NodeIndex == 0).ToArray();
        atNodeZero.Length.ShouldBe(2);
        atNodeZero.ShouldContain(connection => connection.ReferenceBeamIndex == 0 && connection.OtherBeamIndex == 3);
        atNodeZero.ShouldContain(connection => connection.ReferenceBeamIndex == 0 && connection.OtherBeamIndex == 4);
    }
}
