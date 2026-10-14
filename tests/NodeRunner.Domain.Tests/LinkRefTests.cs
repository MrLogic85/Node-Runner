namespace NodeRunner.Domain.Tests;

public sealed class LinkRefTests
{
    [Fact]
    public void Pieces_JoinedByBeamsPistonsAndSprings_AreOne_AndLeaveLooseJointsOut()
    {
        var nodes = Enumerable.Range(1, 5).Select(id => new NodeDef(id, new Vector2D(id * 100, 0))).ToArray();
        var links = LinkRef.All([new BeamDef(11, 1, 2)], [new PistonDef(12, 2, 3)], [new SpringDef(13, 4, 3)]);

        LinkRef.Pieces(nodes, links).ShouldBe([[1, 2, 3, 4]]);
    }

    [Fact]
    public void Pieces_ListEachInNodeOrder_ByItsFirstNode()
    {
        var nodes = new[] { new NodeDef(7, new Vector2D(0, 0)), new NodeDef(3, new Vector2D(100, 0)), new NodeDef(9, new Vector2D(200, 0)), new NodeDef(1, new Vector2D(300, 0)) };
        var links = LinkRef.All([new BeamDef(20, 1, 7), new BeamDef(21, 9, 3)], [], []);

        LinkRef.Pieces(nodes, links).ShouldBe([[7, 1], [3, 9]]);
    }
}
