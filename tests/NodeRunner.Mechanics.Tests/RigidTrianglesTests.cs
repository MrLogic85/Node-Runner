using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class RigidTrianglesTests
{
    [Fact]
    public void Of_ClosedTriangle_ReturnsOneSortedTriangle()
    {
        var creature = new CreatureDef(
            new[]
            {
                new NodeDef(1, new Vector2D(0, 0)),
                new NodeDef(2, new Vector2D(1, 0)),
                new NodeDef(3, new Vector2D(0, 1)),
            },
            new[] { new BeamDef(101, 2, 3), new BeamDef(102, 3, 1), new BeamDef(103, 1, 2) },
            []);

        var triangles = RigidTriangles.Of(creature);

        triangles.ShouldBe([new RigidTriangleDef(0, 1, 2)]);
    }

    [Fact]
    public void Of_Chain_ReturnsNoTriangles()
    {
        var creature = new CreatureDef(
            new[]
            {
                new NodeDef(1, new Vector2D(0, 0)),
                new NodeDef(2, new Vector2D(1, 0)),
                new NodeDef(3, new Vector2D(2, 0)),
            },
            new[] { new BeamDef(101, 1, 2), new BeamDef(102, 2, 3) },
            []);

        var triangles = RigidTriangles.Of(creature);

        triangles.ShouldBeEmpty();
    }

    [Fact]
    public void Of_TwoTrianglesSharingABeam_ReturnsBothInNodeOrder()
    {
        var creature = new CreatureDef(
            new[]
            {
                new NodeDef(1, new Vector2D(0, 0)),
                new NodeDef(2, new Vector2D(1, 0)),
                new NodeDef(3, new Vector2D(1, 1)),
                new NodeDef(4, new Vector2D(0, 1)),
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

        var triangles = RigidTriangles.Of(creature);

        triangles.ShouldBe([new RigidTriangleDef(0, 1, 2), new RigidTriangleDef(0, 2, 3)]);
    }
}
