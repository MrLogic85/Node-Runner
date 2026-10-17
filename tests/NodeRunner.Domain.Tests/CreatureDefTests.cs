using System.Text.Json;

namespace NodeRunner.Domain.Tests;

public sealed class CreatureDefTests
{
    // A small frame: nodes 1 (0,0), 2 (100,0) and 3 (0,100); beams 4 (1–2) and 5 (1–3).
    private static readonly NodeDef[] _nodes = [new(1, new Vector2D(0, 0)), new(2, new Vector2D(100, 0)), new(3, new Vector2D(0, 100))];
    private static readonly BeamDef[] _beams = [new(4, 1, 2), new(5, 1, 3)];

    [Fact]
    public void JsonRoundTrip_KeepsEveryPart_AndALockedBrakeAsInfinity()
    {
        var original = new CreatureDef(
            EveryPartKind.Creature.Parts.Select(part => part is BrakeDef brake ? brake.WithStrength(BrakeDef.LockedStrength).WithName("Hold") : part),
            EveryPartKind.Creature.NextPartId);

        var json = JsonSerializer.Serialize(original);
        var loaded = JsonSerializer.Deserialize<CreatureDef>(json)!;

        loaded.Parts.ShouldBe(original.Parts);
        loaded.NextPartId.ShouldBe(22);
        loaded.Brakes[0].Locked.ShouldBeTrue();
        json.ShouldContain("\"Strength\":\"Infinity\"");
    }

    // Each row uses another of the per-list constructors, so each stays in use.
    [Fact]
    public void Constructor_RefusesBrokenReferencesIdsAndSides()
    {
        Refuses.OutOfRange(() => new CreatureDef(_nodes, [new BeamDef(4, 1, 9)], []));
        Refuses.OutOfRange(() => new CreatureDef(_nodes, _beams, [new SensorDef(6, 9, SensorKind.Accelerometer)]));
        Refuses.Invalid(() => new CreatureDef(_nodes, _beams, [new SensorDef(6, 4, SensorKind.Accelerometer), new SensorDef(7, 4, SensorKind.Camera)]))
            .Message.ShouldBe("Beam id 4 already has a sensor; a beam holds at most one.");
        Refuses.Invalid(() => new CreatureDef(_nodes, [new BeamDef(1, 1, 2)], [], 9)).Message.ShouldBe("Part id 1 is used more than once.");
        Refuses.OutOfRange(() => new CreatureDef(_nodes, _beams, [], 0));
        Refuses.OutOfRange(() => new CreatureDef(_nodes, _beams, [], [new PistonDef(6, 2, 3)], 6));
        Refuses.OutOfRange(() => new CreatureDef(_nodes, _beams, [], [new PistonDef(6, 1, 9)]));
        Refuses.OutOfRange(() => new CreatureDef(_nodes, _beams, [], [], [new SpringDef(6, 9, 1)], 7));
        Refuses.OutOfRange(() => new CreatureDef(_nodes, _beams, [], [], [], [], [new WheelDef(6, 9)], 7));
        Refuses.Invalid(() => new CreatureDef(_nodes, _beams, [], [new ServoDef(6, 2, 4, 5)], [], [], 7))
            .Message.ShouldBe("Link id 5 must touch node id 2, or be its Wheel, for that hinge part.");
        Refuses.Invalid(() => new CreatureDef(_nodes, _beams, [], [], [], [], [new WheelDef(6, 1)], [], [], [], [], [new AngleSensorDef(7, 2, 4, 6)], 8));
        Refuses.Invalid(() => new CreatureDef([_nodes[0], null!], [], []));
        Should.Throw<ArgumentNullException>(() => new CreatureDef(_nodes, null!, []));
        Should.Throw<ArgumentNullException>(() => new CreatureDef(null!, 1));
        Refuses.Invalid(() => new CreatureDef([_nodes[0], new StrayPartDef(2)])).Message.ShouldStartWith("A creature cannot hold a StrayPartDef");

        SecondIsRefused("Wheel", () => new CreatureDef(_nodes, _beams, [], [], [], [], [new WheelDef(6, 1), new WheelDef(7, 1)], 8));
        SecondIsRefused("Touch sensor", () => new CreatureDef(_nodes, _beams, [], [], [], [], [], [new TouchSensorDef(6, 1), new TouchSensorDef(7, 1)], 8));
        SecondIsRefused("Rubber", () => new CreatureDef(_nodes, _beams, [], [], [], [], [], [], [new RubberDef(6, 1), new RubberDef(7, 1)], 8));
        SecondIsRefused("hinge part", () => new CreatureDef(_nodes, _beams, [], [], [], [], [], [], [], [new VelocityMotorDef(6, 1)], [new BrakeDef(7, 1)], 8));

        static void SecondIsRefused(string slot, Action create) =>
            Refuses.Invalid(create).Message.ShouldBe($"Node id 1 already has a {slot}; a joint holds at most one.");
    }

    // #1118: a hinge part's side may be its own joint's Wheel; an unfinished drawing still loads.
    [Fact]
    public void Constructor_AcceptsAWheelSide_AnEmptyDrawing_AndAnUnfinishedOne()
    {
        new CreatureDef(_nodes, _beams, [], [new ServoDef(6, 1, 4, 7)], [], [], [new WheelDef(7, 1)], 8).Servos[0].TargetLinkId.ShouldBe(7);
        var empty = new CreatureDef([], [], []);
        (empty.PartCount, empty.NextPartId).ShouldBe((0, 1));
        new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(0, 0)), new NodeDef(3, new Vector2D(4, 0))], [new BeamDef(4, 1, 2)], [])
            .Beams.Count.ShouldBe(1);
    }

    // Production builds a creature from one list of parts (#1222), in any order; the lists are copies.
    [Fact]
    public void Constructor_FromOnePartList_SortsByKind_KeepsEachKindsOrder_AndCopies()
    {
        var every = EveryPartKind.Creature;
        IPartDef[] shuffled = [.. every.Parts.Reverse()];

        var creature = new CreatureDef(shuffled, every.NextPartId);
        shuffled[0] = new NodeDef(99, new Vector2D(1, 1));

        creature.Parts.ShouldBe(every.Parts.GroupBy(part => part.GetType()).SelectMany(kind => kind.Reverse()));
        new CreatureDef([new NodeDef(7, new Vector2D(0, 0)), new NodeDef(3, new Vector2D(1, 0)), new BeamDef(12, 7, 3)]).NextPartId.ShouldBe(13);
    }

    [Fact]
    public void Queries_OnACreatureOfEveryKind()
    {
        var creature = EveryPartKind.Creature;

        creature.PartCount.ShouldBe(21);
        creature.JointMotors.Select(part => part.Id).ShouldBe([9, 15, 21]);
        creature.HingeParts.Select(part => part.Id).ShouldBe([9, 15, 21, 16, 17]);
        creature.JointPartsAt(1).ShouldBe([
            new CreatureElementSelection(CreatureElementKind.Servo, 9),
            new CreatureElementSelection(CreatureElementKind.Wheel, 12),
            new CreatureElementSelection(CreatureElementKind.Rubber, 14),
            new CreatureElementSelection(CreatureElementKind.TouchSensor, 13),
        ]);
        creature.Sensors[1].Aim.ShouldBe(-Math.PI / 2);
        creature.Sensors[0].Aim.ShouldBeNull();
        new CreatureDef(_nodes, _beams, [new SensorDef(6, 4, SensorKind.Camera, aim: 2)]).Sensors[0].Aim.ShouldBe(2);

        // #1086: a Wheel widens its joint but covers none of the links; only a joint motor does.
        foreach (var (node, radius, covered) in new[] { (1, 40.0, 27.0), (2, 27, 27), (3, 15, 15), (4, 15, 15), (18, 27, 27) })
        {
            (creature.NodeRadius(node), creature.CoveredRadius(node)).ShouldBe((radius, covered), $"node {node}");
        }

        (creature.NodeIndexOf(18), creature.BeamIndexOf(20), creature.SensorIndexOf(8)).ShouldBe((4, 3, 1));
        new[]
        {
            creature.ServoIndexOf(9), creature.PistonIndexOf(10), creature.SpringIndexOf(11), creature.WheelIndexOf(12), creature.TouchSensorIndexOf(13),
            creature.RubberIndexOf(14), creature.VelocityMotorIndexOf(15), creature.BrakeIndexOf(16), creature.AngleSensorIndexOf(17), creature.StepperIndexOf(21),
        }.ShouldAllBe(index => index == 0);
        Refuses.OutOfRange(() => creature.NodeIndexOf(5));
        Refuses.OutOfRange(() => creature.CoveredRadius(99));
        foreach (var part in creature.Parts)
        {
            creature.FindPart(part.Id).ShouldBeSameAs(part);
        }

        creature.FindPart(22).ShouldBeNull();
        creature.WheelAt(1)!.Id.ShouldBe(12);
        creature.WheelAt(2).ShouldBeNull();
        (creature.IsWheel(12), creature.IsWheel(13)).ShouldBe((true, false));
        creature.LinksAt(4).Select(link => (link.Id, link.Kind)).ShouldBe([(19, CreatureElementKind.Beam), (10, CreatureElementKind.Piston), (11, CreatureElementKind.Spring)]);
        (creature.Link(19).FarNodeFrom(4), creature.Link(19).FarNodeFrom(18)).ShouldBe((18, 4));
        Refuses.Invalid(() => creature.Link(19).FarNodeFrom(1));
        Refuses.OutOfRange(() => creature.Link(99));
        (CreatureDef.HasJointPart(3, creature.Parts), CreatureDef.HasJointPart(99, creature.Parts)).ShouldBe((true, false));
    }

    // A Brake sits in its joint's hinge-part slot, at the top of the stack (#1041), whatever the list order (#1221).
    [Fact]
    public void JointParts_PutTheHingePartFirst_AndLeaveOtherJointsOut()
    {
        CreatureDef.JointParts(1, [new WheelDef(8, 1), new BrakeDef(6, 1), new BrakeDef(7, 2)]).ShouldBe([
            new CreatureElementSelection(CreatureElementKind.Brake, 6),
            new CreatureElementSelection(CreatureElementKind.Wheel, 8),
        ]);
        CreatureDef.JointParts(4, [new WheelDef(8, 1)]).ShouldBeEmpty();
    }

    // #1118: a motor needs two sides, and a Wheel can be one of them.
    [Fact]
    public void JointMotor_CountsTheWheelAsASide()
    {
        LinkRef[] links = [new(9, 1, 3, CreatureElementKind.Beam), new(4, 1, 2, CreatureElementKind.Spring), new(7, 1, 4, CreatureElementKind.Piston)];
        var wheel = new WheelDef(5, 1);

        (JointMotor.HasTwoSides(links[..1], true), JointMotor.HasTwoSides(links[..1], false), JointMotor.HasTwoSides([], true)).ShouldBe((true, false, false));
        JointMotor.DefaultSides(links, wheel).ShouldBe((4, 7));
        JointMotor.DefaultSides(links[..1], wheel).ShouldBe((9, 5));
        JointMotor.DefaultSides([], wheel).ShouldBe((null, 5));
        JointMotor.DefaultSides(links[..1], null).ShouldBe((9, null));
    }

    private sealed record StrayPartDef(int Id) : IPartDef
    {
        public string? Name => null;

        public PartKind PartKind => PartKind.Node;
    }
}
