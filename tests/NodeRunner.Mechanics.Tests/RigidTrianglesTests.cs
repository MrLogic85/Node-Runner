using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class RigidTrianglesTests
{
    // By node index, sorted: a closed triangle is one, a chain none, and a square with a diagonal two in node order.
    [Fact]
    public void Of_FindsEachTriangleOfBeams_InNodeOrder()
    {
        Vector2D[] square = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];
        foreach (var (beams, triangles) in new (int[][], RigidTriangleDef[])[]
        {
            ([[2, 3], [3, 1], [1, 2]], [new(0, 1, 2)]),
            ([[1, 2], [2, 3]], []),
            ([[1, 2], [2, 3], [3, 4], [4, 1], [1, 3]], [new(0, 1, 2), new(0, 2, 3)]),
        })
        {
            var creature = new CreatureDef(
                square.Select((at, index) => new NodeDef(index + 1, at)).ToArray(),
                beams.Select((ends, index) => new BeamDef(101 + index, ends[0], ends[1])).ToArray(),
                []);
            RigidTriangles.Of(creature).ShouldBe(triangles);
        }
    }
}
