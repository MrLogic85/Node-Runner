namespace NodeRunner.Domain.Tests;

/// <summary>The back-to-front draw groups and ranks of a creature's parts (#1107), shared by the drawing and by touches.</summary>
public sealed class DrawGroupsTests
{
    // A frame: a top beam, two legs down to feet, and a joint joined to nothing.
    private static readonly NodeDef[] _nodes =
    [
        new(1, new Vector2D(0, 0)),
        new(2, new Vector2D(100, 0)),
        new(3, new Vector2D(0, 100)),
        new(4, new Vector2D(100, 100)),
        new(5, new Vector2D(300, 300)),
    ];

    private static readonly LinkRef[] _links =
    [
        new(12, 2, 4, CreatureElementKind.Beam),
        new(10, 1, 2, CreatureElementKind.Beam),
        new(11, 1, 3, CreatureElementKind.Piston),
    ];

    private static DrawGroups Frame() => DrawGroups.Of(_nodes, _links);

    [Fact]
    public void Links_draw_from_the_highest_lower_end_down_ties_by_id()
    {
        var groups = Frame();

        groups.Link(10).ShouldBe(0);
        groups.Link(11).ShouldBe(1);
        groups.Link(12).ShouldBe(2);
        groups.Link(99).ShouldBeNull();
    }

    [Fact]
    public void A_joint_draws_under_its_first_link_and_over_its_last()
    {
        var groups = Frame();

        groups.Joint(1, DrawSlot.Under).ShouldBe(0);
        groups.Joint(1, DrawSlot.Over).ShouldBe(1);
        groups.Joint(4, DrawSlot.Under).ShouldBe(2);
        groups.Joint(5, DrawSlot.Over).ShouldBeNull();
    }

    private static CreatureElementSelection Part(CreatureElementKind kind, int id) => new(kind, id);

    [Fact]
    public void A_selected_joint_raises_its_links_and_their_ends_and_a_selected_link_its_ends()
    {
        var groups = Frame();

        var raised = groups.Raised([Part(CreatureElementKind.Node, 3)], [], []);
        raised.Links.ShouldBe([11]);
        raised.Joints.ShouldBe([1, 3], ignoreOrder: true);

        raised = groups.Raised([Part(CreatureElementKind.Beam, 12)], [], []);
        raised.Links.ShouldBe([12]);
        raised.Joints.ShouldBe([2, 4], ignoreOrder: true);
    }

    [Fact]
    public void A_selected_Servo_or_Wheel_raises_as_its_joint()
    {
        var groups = Frame();

        groups.Raised([Part(CreatureElementKind.Servo, 30)], [new ServoDef(30, 3)], []).Joints.ShouldBe([1, 3], ignoreOrder: true);
        groups.Raised([Part(CreatureElementKind.Wheel, 40)], [], [new WheelDef(40, 4)]).Links.ShouldBe([12]);
    }

    [Fact]
    public void A_link_whose_ends_both_rose_rises_too_so_a_raised_Wheel_never_hides_it()
    {
        var raised = Frame().Raised([Part(CreatureElementKind.Node, 3), Part(CreatureElementKind.Node, 4)], [], []);

        raised.Links.ShouldBe([10, 11, 12], ignoreOrder: true);
        raised.Sensor(new SensorDef(7, 10, SensorKind.Camera)).ShouldBeTrue();
    }

    [Fact]
    public void Targets_rise_alone_and_a_sensor_rises_selected_or_with_its_beam()
    {
        var raised = Frame().Raised([Part(CreatureElementKind.Sensor, 7)], [], [], targetLinks: [12], targetJoints: [5]);

        raised.Links.ShouldBe([12]);
        raised.Joints.ShouldBe([5]);
        raised.Sensor(new SensorDef(7, 10, SensorKind.Accelerometer)).ShouldBeTrue();
        raised.Sensor(new SensorDef(8, 12, SensorKind.Accelerometer)).ShouldBeTrue();
        raised.Sensor(new SensorDef(9, 10, SensorKind.Accelerometer)).ShouldBeFalse();

        Frame().Raised([], [], [], targetJoints: [1, 2]).Links.ShouldBeEmpty();
    }

    // #1107: a touch hits the part drawn on top, by the same groups and surfaces the drawing uses.
    // A cross: beam 20 from (-60,50) to (60,50) and a link 21 from (0,0) to (0,100), whose lower end
    // is lower, so it draws over the beam.
    private static readonly NodeDef[] _crossNodes =
    [
        new(1, new Vector2D(0, 0)),
        new(2, new Vector2D(0, 100)),
        new(3, new Vector2D(-60, 50)),
        new(4, new Vector2D(60, 50)),
    ];

    private static CreatureElementSelection? HitCross(
        Vector2D at,
        CreatureElementKind kind = CreatureElementKind.Piston,
        RaisedParts? raised = null,
        ServoDef[]? servos = null,
        WheelDef[]? wheels = null,
        SensorDef[]? sensors = null,
        NodeDef[]? nodes = null)
    {
        nodes ??= _crossNodes;
        var groups = DrawGroups.Of(nodes, [new(20, 3, 4, CreatureElementKind.Beam), new(21, 1, 2, kind)]);
        var positions = nodes.ToDictionary(node => node.Id, node => node.Position);
        return groups.PartAt(at, id => positions[id], servos ?? [], wheels ?? [], sensors ?? [], raised ?? RaisedParts.None);
    }

    [Theory]
    [InlineData(CreatureElementKind.Piston)]
    [InlineData(CreatureElementKind.Spring)]
    public void A_link_drawn_lower_is_hit_over_a_beam_it_crosses_and_a_beam_drawn_lower_over_it(CreatureElementKind kind)
    {
        HitCross(new Vector2D(0, 50), kind).ShouldBe(new CreatureElementSelection(kind, 21));

        // Moved below the link's lower end, the beam draws last and takes the crossing.
        NodeDef[] beamLower = [_crossNodes[0], _crossNodes[1], new(3, new Vector2D(-60, 30)), new(4, new Vector2D(60, 120))];
        var crossing = new Vector2D(0, 75);
        HitCross(crossing, kind, nodes: beamLower).ShouldBe(new CreatureElementSelection(CreatureElementKind.Beam, 20));
    }

    [Fact]
    public void A_raised_part_is_hit_over_one_drawn_over_it_on_the_unselected_surface()
    {
        var crossing = new Vector2D(0, 50);

        HitCross(crossing, raised: Frame0().Raised([Part(CreatureElementKind.Beam, 20)], [], [])).ShouldBe(new CreatureElementSelection(CreatureElementKind.Beam, 20));
        HitCross(crossing, raised: Frame0().Raised([Part(CreatureElementKind.Node, 3)], [], [])).ShouldBe(new CreatureElementSelection(CreatureElementKind.Beam, 20));

        static DrawGroups Frame0() => DrawGroups.Of(_crossNodes, [new(20, 3, 4, CreatureElementKind.Beam), new(21, 1, 2, CreatureElementKind.Piston)]);
    }

    [Fact]
    public void A_joints_ring_or_Servo_is_hit_to_its_halo_over_its_links()
    {
        var plainHalo = SelectionMarks.JointHalo(NodeDef.PlainJointRadius);
        HitCross(new Vector2D(0, plainHalo)).ShouldBe(new CreatureElementSelection(CreatureElementKind.Node, 1));
        HitCross(new Vector2D(0, plainHalo + 1)).ShouldBe(new CreatureElementSelection(CreatureElementKind.Piston, 21));

        var servoHalo = SelectionMarks.JointHalo(ServoDef.JointRadius);
        ServoDef[] servos = [new(30, 1)];
        HitCross(new Vector2D(0, servoHalo), servos: servos).ShouldBe(new CreatureElementSelection(CreatureElementKind.Servo, 30));
        HitCross(new Vector2D(0, servoHalo + 1), servos: servos).ShouldBe(new CreatureElementSelection(CreatureElementKind.Piston, 21));
    }

    [Fact]
    public void A_link_leaves_its_own_ends_reach_to_them_even_drawn_over_them()
    {
        // The beam draws over its own Wheel and, raised as a target, over the unraised ring at its end.
        var onBeamNearItsEnd = new Vector2D(-55, 50);
        HitCross(onBeamNearItsEnd, wheels: [new WheelDef(40, 3)]).ShouldBe(new CreatureElementSelection(CreatureElementKind.Wheel, 40));

        var raised = DrawGroups.Of(_crossNodes, [new(20, 3, 4, CreatureElementKind.Beam), new(21, 1, 2, CreatureElementKind.Piston)]).Raised([], [], [], targetLinks: [20]);
        HitCross(onBeamNearItsEnd, raised: raised).ShouldBe(new CreatureElementSelection(CreatureElementKind.Node, 3));
        HitCross(new Vector2D(-30, 50), raised: raised).ShouldBe(new CreatureElementSelection(CreatureElementKind.Beam, 20));
    }

    [Fact]
    public void A_Wheels_face_is_hit_under_its_links_and_a_sensor_over_its_beam()
    {
        WheelDef[] wheels = [new(40, 3)];
        HitCross(new Vector2D(-60, 50 + WheelDef.DefaultRadius), wheels: wheels).ShouldBe(new CreatureElementSelection(CreatureElementKind.Wheel, 40));

        SensorDef[] sensors = [new(50, 20, SensorKind.Camera)];
        HitCross(new Vector2D(15, 50), sensors: sensors).ShouldBe(new CreatureElementSelection(CreatureElementKind.Sensor, 50));
        HitCross(new Vector2D(300, 300), sensors: sensors).ShouldBeNull();
    }

    [Fact]
    public void Ranks_go_slot_by_slot_group_by_group_then_loose_joints_then_the_selected_surface()
    {
        int[] order =
        [
            DrawGroups.Rank(DrawSlot.Under, 0, selected: false),
            DrawGroups.Rank(DrawSlot.Link, 0, selected: false),
            DrawGroups.Rank(DrawSlot.Sensor, 0, selected: false),
            DrawGroups.Rank(DrawSlot.Over, 0, selected: false),
            DrawGroups.Rank(DrawSlot.Under, 1, selected: false),
            DrawGroups.Rank(DrawSlot.Over, DrawGroups.MaxGroups - 1, selected: false),
            DrawGroups.Rank(DrawSlot.Over, group: null, selected: false),
            DrawGroups.Rank(DrawSlot.Under, 0, selected: true),
            DrawGroups.Rank(DrawSlot.Over, group: null, selected: true),
        ];

        order.ShouldBeInOrder(SortDirection.Ascending);
        order.Distinct().Count().ShouldBe(order.Length);
        order[0].ShouldBe(0);
        order[^1].ShouldBe((2 * DrawGroups.SurfaceRanks) - 1);
    }

    [Fact]
    public void Groups_past_the_last_share_it() =>
        DrawGroups.Rank(DrawSlot.Link, DrawGroups.MaxGroups + 7, selected: false).ShouldBe(DrawGroups.Rank(DrawSlot.Link, DrawGroups.MaxGroups - 1, selected: false));
}
