using static NodeRunner.Domain.CreatureElementKind;

namespace NodeRunner.Domain.Tests;

/// <summary>The back-to-front draw groups and ranks of a creature's parts, shared by the drawing and by touches.</summary>
public sealed class DrawGroupsTests
{
    // A frame: a top beam 10 (1–2), a Piston leg 11 (1–3), a beam leg 12 (2–4) and a joint 5 joined to nothing.
    private static readonly DrawGroups _frame = DrawGroups.Of(
        [new(1, new Vector2D(0, 0)), new(2, new Vector2D(100, 0)), new(3, new Vector2D(0, 100)), new(4, new Vector2D(100, 100)), new(5, new Vector2D(300, 300))],
        [new(12, 2, 4, Beam), new(10, 1, 2, Beam), new(11, 1, 3, Piston)]);

    // A cross: beam 20 from (-60,50) to (60,50) and link 21 from (0,0) to (0,100), whose lower end is lower, so it draws over the beam.
    private static readonly NodeDef[] _crossNodes =
        [new(1, new Vector2D(0, 0)), new(2, new Vector2D(0, 100)), new(3, new Vector2D(-60, 50)), new(4, new Vector2D(60, 50))];

    private static readonly DrawGroups _cross = CrossOf(_crossNodes, Piston);

    private static readonly double _plainHalo = SelectionMarks.JointHalo(NodeDef.PlainJointRadius);
    private static readonly double _motorHalo = SelectionMarks.JointHalo(JointMotor.Radius);

    private static CreatureElementSelection P(CreatureElementKind kind, int id) => new(kind, id);

    private static DrawGroups CrossOf(NodeDef[] nodes, CreatureElementKind kind) => DrawGroups.Of(nodes, [new(20, 3, 4, Beam), new(21, 1, 2, kind)]);

    private static CreatureElementSelection? HitCross(
        Vector2D at, CreatureElementKind kind = Piston, RaisedParts? raised = null, IHingePartDef[]? hingeParts = null,
        WheelDef[]? wheels = null, SensorDef[]? sensors = null, NodeDef[]? nodes = null)
    {
        nodes ??= _crossNodes;
        var positions = nodes.ToDictionary(node => node.Id, node => node.Position);
        return CrossOf(nodes, kind).PartAt(at, id => positions[id], hingeParts ?? [], wheels ?? [], sensors ?? [], raised ?? RaisedParts.None);
    }

    // Links draw from the highest lower end down, ties by id; a joint draws under its first link and over its last,
    // its bands with its ring and hinge part over every link at it.
    [Fact]
    public void Groups_OrderLinksAndJoints()
    {
        (_frame.Link(10), _frame.Link(11), _frame.Link(12), _frame.Link(99)).ShouldBe((0, 1, 2, null));
        (_frame.Joint(1, DrawSlot.Under), _frame.Joint(1, DrawSlot.Over), _frame.Joint(1, DrawSlot.Band)).ShouldBe((0, 1, 1));
        (_frame.Joint(4, DrawSlot.Under), _frame.Joint(5, DrawSlot.Over)).ShouldBe((2, null));
    }

    // A selected joint raises its links and their ends, a link its ends, and a joint part its joint; a Wheel's face rises
    // only with its own selection. A link whose ends both rose rises too, so a raised Servo never hides it.
    [Fact]
    public void Raised_FollowsTheSelectionToItsJointsAndLinks()
    {
        var byJoint = _frame.Raised([P(Node, 3)], [], [], [], []);
        byJoint.Links.ShouldBe([11]);
        byJoint.Joints.ShouldBe([1, 3], ignoreOrder: true);
        WheelDef[] wheels = [new(40, 4)];
        var byLink = _frame.Raised([P(Beam, 12)], [], wheels, [], []);
        byLink.Links.ShouldBe([12]);
        byLink.Joints.ShouldBe([2, 4], ignoreOrder: true);
        byLink.Wheels.ShouldBeEmpty();
        var byWheel = _frame.Raised([P(Wheel, 40)], [], wheels, [], []);
        byWheel.Links.ShouldBe([12]);
        byWheel.Wheels.ShouldBe([4]);
        _frame.Raised([], [], wheels, [], [], targetJoints: [4]).Wheels.ShouldBeEmpty();

        foreach (var (part, hinge, touch, rubber) in new (CreatureElementSelection, IHingePartDef?, TouchSensorDef?, RubberDef?)[]
        {
            (P(Servo, 30), new ServoDef(30, 3), null, null), (P(VelocityMotor, 70), new VelocityMotorDef(70, 3), null, null),
            (P(Brake, 80), new BrakeDef(80, 3), null, null), (P(Stepper, 90), new StepperDef(90, 3), null, null),
            (P(TouchSensor, 50), null, new TouchSensorDef(50, 3), null), (P(Rubber, 60), null, null, new RubberDef(60, 3)),
        })
        {
            _frame.Raised([part], hinge is null ? [] : [hinge], [], touch is null ? [] : [touch], rubber is null ? [] : [rubber])
                .Joints.Order().ToArray().ShouldBe([1, 3], $"{part.Kind}");
        }

        var both = _frame.Raised([P(Node, 3), P(Node, 4)], [], [], [], []);
        both.Links.Order().ShouldBe([10, 11, 12]);
        both.Sensor(new SensorDef(7, 10, SensorKind.Camera)).ShouldBeTrue();
    }

    [Fact]
    public void Raised_Targets_RiseAlone_AndASensorRisesSelectedOrWithItsBeam()
    {
        var raised = _frame.Raised([P(Sensor, 7)], [], [], [], [], targetLinks: [12], targetJoints: [5]);

        raised.Links.ShouldBe([12]);
        raised.Joints.ShouldBe([5]);
        raised.Sensor(new SensorDef(7, 10, SensorKind.Accelerometer)).ShouldBeTrue();
        raised.Sensor(new SensorDef(8, 12, SensorKind.Accelerometer)).ShouldBeTrue();
        raised.Sensor(new SensorDef(9, 10, SensorKind.Accelerometer)).ShouldBeFalse();
        _frame.Raised([], [], [], [], [], targetJoints: [1, 2]).Links.ShouldBeEmpty();
    }

    // A touch hits the part drawn on top, by the same groups and surfaces the drawing uses.
    [Fact]
    public void PartAt_HitsTheLinkDrawnOnTop_OrTheRaisedOne()
    {
        HitCross(new Vector2D(0, 50)).ShouldBe(P(Piston, 21));
        HitCross(new Vector2D(0, 50), Spring).ShouldBe(P(Spring, 21));
        NodeDef[] beamLower = [_crossNodes[0], _crossNodes[1], new(3, new Vector2D(-60, 30)), new(4, new Vector2D(60, 120))];
        HitCross(new Vector2D(0, 75), Spring, nodes: beamLower).ShouldBe(P(Beam, 20));
        HitCross(new Vector2D(0, 50), raised: _cross.Raised([P(Beam, 20)], [], [], [], [])).ShouldBe(P(Beam, 20));
        HitCross(new Vector2D(0, 50), raised: _cross.Raised([P(Node, 3)], [], [], [], [])).ShouldBe(P(Beam, 20));
    }

    // A ring or Servo to its halo, a Stepper at a motor's size over its Wheel's hub and inside a Touch sensor's
    // cushion, a Brake at a plain joint's size over its Wheel's hub.
    [Fact]
    public void PartAt_HitsAJointOrHingePart_ToItsHalo_OverItsLinks()
    {
        HitCross(new Vector2D(0, _plainHalo)).ShouldBe(P(Node, 1));
        HitCross(new Vector2D(0, _plainHalo + 1)).ShouldBe(P(Piston, 21));
        foreach (var (hinge, halo) in new (IHingePartDef, double)[] { (new ServoDef(30, 1), _motorHalo), (new VelocityMotorDef(70, 1), _motorHalo), (new StepperDef(90, 1), _motorHalo), (new BrakeDef(80, 1), _plainHalo) })
        {
            var part = CreatureElementSelection.Of(hinge);
            HitCross(new Vector2D(0, halo), hingeParts: [hinge]).ShouldBe(part);
            HitCross(new Vector2D(0, halo + 1), hingeParts: [hinge]).ShouldBe(P(Piston, 21), $"{part.Kind}");
            HitCross(new Vector2D(0, NodeDef.HubRadius), hingeParts: [hinge], wheels: [new(40, 1)]).ShouldBe(part);
        }

        StepperDef[] steppers = [new(90, 1)];
        var touched = _cross.Raised([P(TouchSensor, 50)], steppers, [], [new TouchSensorDef(50, 1)], []);
        HitCross(new Vector2D(0, _motorHalo), raised: touched, hingeParts: steppers).ShouldBe(P(Stepper, 90));
    }

    // A link draws hub to hub, over its own Wheel's face and under its hub; raised as a target, it draws over
    // the unraised ring at its end. A Wheel is hit only at its hub; a touch on its tyre or middle hits what lies under it.
    [Fact]
    public void PartAt_HitsALinkOutFromItsEndsHubs_AndAWheelOnlyAtItsHub()
    {
        WheelDef[] wheels = [new(40, 3)];
        var hubHalo = SelectionMarks.JointHalo(NodeDef.HubRadius);
        HitCross(new Vector2D(-60 + hubHalo + 1, 50), wheels: wheels).ShouldBe(P(Beam, 20));
        HitCross(new Vector2D(-60 + hubHalo, 50), wheels: wheels).ShouldBe(P(Wheel, 40));
        var target = _cross.Raised([], [], [], [], [], targetLinks: [20]);
        HitCross(new Vector2D(-60 + NodeDef.HubRadius - 1, 50), raised: target).ShouldBe(P(Node, 3));
        HitCross(new Vector2D(-60 + NodeDef.HubRadius + 1, 50), raised: target).ShouldBe(P(Beam, 20));
        var onHub = new Vector2D(-60 + NodeDef.HubRadius + 1, 50);
        HitCross(onHub, raised: _cross.Raised([P(Beam, 20)], [], wheels, [], []), wheels: wheels).ShouldBe(P(Wheel, 40));
        HitCross(onHub, wheels: wheels, hingeParts: [new ServoDef(30, 3)]).ShouldBe(P(Servo, 30));

        WheelDef[] big = [new(40, 1, radius: 100)];
        HitCross(new Vector2D(-30, 50), raised: _cross.Raised([P(Wheel, 40)], [], big, [], []), wheels: big).ShouldBe(P(Beam, 20));
        HitCross(new Vector2D(-50, -50), wheels: big).ShouldBeNull();
        HitCross(new Vector2D(0, -NodeDef.HubRadius), wheels: big).ShouldBe(P(Wheel, 40));
        HitCross(new Vector2D(15, 50), sensors: [new(50, 20, SensorKind.Camera)]).ShouldBe(P(Sensor, 50));
        HitCross(new Vector2D(300, 300), sensors: [new(50, 20, SensorKind.Camera)]).ShouldBeNull();
    }

    // Wheel 41 round (0, 0) and Wheel 40 round (100, 0), both 1 m, overlap: the nearest hub wins, then the lower id.
    [Fact]
    public void WheelDiscAt_HoldsAPointOutToItsRadius()
    {
        Dictionary<int, Vector2D> hubs = new() { [1] = new(0, 0), [2] = new(100, 0) };
        WheelDef[] wheels = [new(41, 1, radius: 100), new(40, 2, radius: 100)];

        Vector2D[] points = [new(0, -100), new(0, -101), new(40, 60), new(60, 60), new(50, 60)];
        points.Select(at => DrawGroups.WheelDiscAt(at, id => hubs[id], wheels)?.Id).ShouldBe([41, null, 41, 40, 40]);
    }

    // Slot by slot, group by group, then loose joints, then the selected surface; groups past the last share it.
    [Fact]
    public void Rank_OrdersEverySurface()
    {
        int[] order =
        [
            DrawGroups.Rank(DrawSlot.Under, 0, false), DrawGroups.Rank(DrawSlot.Link, 0, false), DrawGroups.Rank(DrawSlot.Sensor, 0, false),
            DrawGroups.Rank(DrawSlot.Band, 0, false), DrawGroups.Rank(DrawSlot.Over, 0, false), DrawGroups.Rank(DrawSlot.Under, 1, false),
            DrawGroups.Rank(DrawSlot.Over, DrawGroups.MaxGroups - 1, false), DrawGroups.Rank(DrawSlot.Under, null, false),
            DrawGroups.Rank(DrawSlot.Band, null, false), DrawGroups.Rank(DrawSlot.Over, null, false),
            DrawGroups.Rank(DrawSlot.Under, 0, true), DrawGroups.Rank(DrawSlot.Over, null, true),
        ];

        order.ShouldBeInOrder(SortDirection.Ascending);
        order.Distinct().Count().ShouldBe(order.Length);
        (order[0], order[^1]).ShouldBe((0, (2 * DrawGroups.SurfaceRanks) - 1));
        DrawGroups.Rank(DrawSlot.Link, DrawGroups.MaxGroups + 7, false).ShouldBe(DrawGroups.Rank(DrawSlot.Link, DrawGroups.MaxGroups - 1, false));
    }

    // Each part kind draws in its owner's group: a joint part its joint's, a sensor its beam's.
    [Fact]
    public void OwnerOf_EveryPartKind_IsItsJointLinkOrBeam()
    {
        var parts = EveryPartKind.Creature.Parts.ToDictionary(part => part.Id);

        EveryPartKind.HoldsEveryKind.ShouldBeTrue();
        parts.Values.ToDictionary(part => part.Id, DrawGroups.OwnerOf).ShouldBe(new Dictionary<int, int>
        {
            [1] = 1, [2] = 2, [3] = 3, [4] = 4, [18] = 18, [5] = 5, [6] = 6, [19] = 19, [20] = 20, [7] = 5, [8] = 6, [9] = 1,
            [10] = 10, [11] = 11, [12] = 1, [13] = 1, [14] = 1, [15] = 2, [16] = 3, [17] = 4, [21] = 18,
        });
    }
}
