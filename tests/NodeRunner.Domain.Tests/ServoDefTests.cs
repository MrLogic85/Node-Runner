namespace NodeRunner.Domain.Tests;

public sealed class ServoDefTests
{
    [Fact]
    public void Constructor_ValidatesItsSettings()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new ServoDef(0, 1, 2, 3));
        Should.Throw<ArgumentOutOfRangeException>(() => new ServoDef(1, 1, 2, 3, strength: 0));
        Should.Throw<ArgumentOutOfRangeException>(() => new ServoDef(1, 1, 2, 3, range: 10 * Math.PI / 180));
        Should.Throw<ArgumentOutOfRangeException>(() => new ServoDef(1, 1, 2, 3, start: 1.1));
        Should.Throw<ArgumentOutOfRangeException>(() => new ServoDef(1, 1, 2, 3, maxSpeed: 0));
        Should.Throw<ArgumentOutOfRangeException>(() => new ServoDef(1, 1, 2, 3, riseTime: 0));
        Should.Throw<ArgumentException>(() => new ServoDef(1, 1, 2, 2));
    }

    [Fact]
    public void CreatureDef_AllowsMissingServoLinks_ButRejectsWrongNodeOrSecondServo()
    {
        var nodes = new[] { new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(1, 0)), new NodeDef(3, new Vector2D(0, 1)) };
        var beams = new[] { new BeamDef(4, 1, 2), new BeamDef(5, 1, 3) };

        var creature = new CreatureDef(nodes, beams, [], [new ServoDef(6, 1, null, 5)], [], [], nextPartId: 7);

        creature.Servos.Single().FixedLinkId.ShouldBeNull();
        creature.NodeRadius(1).ShouldBe(ServoDef.JointRadius);
        Should.Throw<ArgumentException>(() => new CreatureDef(nodes, beams, [], [new ServoDef(6, 2, 4, 5)], [], [], nextPartId: 7));
        Should.Throw<ArgumentException>(() => new CreatureDef(nodes, beams, [], [new ServoDef(6, 1, 4, 5), new ServoDef(7, 1, 4, 5)], [], [], nextPartId: 8));
    }

    [Fact]
    public void CreatureDef_ServoLinks_AcceptPistonsAndSpringsTouchingTheJoint()
    {
        var nodes = new[]
        {
            new NodeDef(1, new Vector2D(0, 0)),
            new NodeDef(2, new Vector2D(1, 0)),
            new NodeDef(3, new Vector2D(0, 1)),
            new NodeDef(4, new Vector2D(2, 0)),
        };
        var pistons = new[] { new PistonDef(5, 1, 2), new PistonDef(7, 2, 4) };
        var springs = new[] { new SpringDef(6, 1, 3) };

        var creature = new CreatureDef(nodes, [], [], [new ServoDef(8, 1, 5, 6)], pistons, springs, nextPartId: 9);

        creature.Servos.Single().FixedLinkId.ShouldBe(5);
        creature.Servos.Single().TargetLinkId.ShouldBe(6);
        Should.Throw<ArgumentException>(() => new CreatureDef(nodes, [], [], [new ServoDef(8, 1, 5, 7)], pistons, springs, nextPartId: 9));
    }
}
