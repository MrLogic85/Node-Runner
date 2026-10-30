using NodeRunner.App.Builders;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Builders;

public sealed class CreatureBuilderTests
{
    private static readonly CreatureElementKind[] _hingeKinds =
    [
        CreatureElementKind.Servo,
        CreatureElementKind.VelocityMotor,
        CreatureElementKind.Stepper,
        CreatureElementKind.Brake,
        CreatureElementKind.AngleSensor,
    ];

    // Every hinge part fills its joint's one hinge-part slot and takes its sides by the Servo's rules;
    // every side change keeps its id, and so its ports and training.
    [Theory]
    [InlineData(CreatureElementKind.Servo)]
    [InlineData(CreatureElementKind.VelocityMotor)]
    [InlineData(CreatureElementKind.Stepper)]
    [InlineData(CreatureElementKind.Brake)]
    [InlineData(CreatureElementKind.AngleSensor)]
    public void AHingePart_TakesItsSidesByTheServosRules_AndKeepsItsIdThroughEveryChange(CreatureElementKind kind)
    {
        var builder = Joint(out var joint, out var far, out var first, out var second);
        var elsewhere = builder.AddBeam(far, builder.AddNode(new Vector2D(240, 0)));
        var wheelJoint = builder.AddNode(new Vector2D(120, -120));
        var wheelBeam = builder.AddBeam(wheelJoint, far);
        CanAddHingePart(builder, kind, 999, out var offJoint).ShouldBeFalse();
        offJoint.ShouldBe(OffJointReason(kind));
        Should.Throw<ArgumentException>(() => AddHingePart(builder, kind, 999)).Message.ShouldBe(offJoint!.Message);
        Should.Throw<ArgumentException>(() => AddHingePart(builder, kind, joint, first, elsewhere));

        var part = AddHingePart(builder, kind, joint);

        ShouldHaveSides(builder, part, first, second);
        new CreatureBuilder(builder.Build()).HingeParts.ShouldBe(builder.HingeParts);
        foreach (var other in _hingeKinds)
        {
            CanAddHingePart(builder, other, joint, out var full).ShouldBeFalse();
            full.ShouldBe(CreatureBuilder.OneHingePartPerJointReason);
        }

        Should.Throw<ArgumentException>(() => AddHingePart(builder, kind, joint));
        builder.Rename(part, "Knee");
        builder.SetHingePartLink(part, fixedRole: true, second);
        ShouldHaveSides(builder, part, second, first);
        builder.SetHingePartLink(part, fixedRole: false, second);
        ShouldHaveSides(builder, part, first, second);
        Should.Throw<ArgumentException>(() => builder.SetHingePartLink(part, fixedRole: true, elsewhere));
        var spring = builder.AddSpring(joint, far);
        ShouldHaveSides(builder, part, spring, second);
        var third = builder.AddBeam(joint, builder.AddNode(new Vector2D(-120, 0)));
        builder.SetHingePartLink(part, fixedRole: false, third);
        ShouldHaveSides(builder, part, spring, third);
        builder.SetHingePartLink(part, fixedRole: true, second);
        ShouldHaveSides(builder, part, second, third);
        builder.RemovePart(third);
        ShouldHaveSides(builder, part, second, null);
        builder.HingeParts.Single().Name.ShouldBe("Knee");
        builder.RemovePart(part);
        builder.HingeParts.ShouldBeEmpty();
        builder.CanAddServo(joint, out _).ShouldBeTrue();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.RemovePart(part));

        // A Wheel is a one-sided link at its joint: a side until it goes.
        var wheel = builder.AddWheel(wheelJoint);
        var onWheel = AddHingePart(builder, kind, wheelJoint);
        ShouldHaveSides(builder, onWheel, wheelBeam, wheel);
        builder.RemovePart(wheel);
        ShouldHaveSides(builder, onWheel, wheelBeam, null);
        builder.RemoveNode(wheelJoint);
        builder.HingeParts.ShouldBeEmpty();
    }

    // AddHingePart's subset switch takes every kind in the hinge-part slot and refuses every other.
    [Fact]
    public void AddHingePart_TakesEveryHingePartKind_AndRefusesEveryOther()
    {
        foreach (var info in PartKinds.All)
        {
            var builder = Joint(out var joint, out _, out var first, out var second);

            if (info.Slot == JointSlot.HingePart)
            {
                builder.FindPart(builder.AddHingePart(info.Kind, joint, first, second))!.PartKind.ShouldBe(info.Kind);
            }
            else
            {
                Should.Throw<ArgumentOutOfRangeException>(() => builder.AddHingePart(info.Kind, joint, first, second));
            }
        }
    }

    // A Wheel, a Touch sensor and a Rubber each take one slot of their own at a joint, beside a hinge part.
    [Theory]
    [InlineData(CreatureElementKind.Wheel)]
    [InlineData(CreatureElementKind.TouchSensor)]
    [InlineData(CreatureElementKind.Rubber)]
    public void AJointPart_TakesItsOwnSlot_AndGoesWithItsJoint(CreatureElementKind kind)
    {
        var builder = PairBuilder();
        var (joint, other, beam) = (builder.Nodes[0].Id, builder.Nodes[1].Id, builder.Beams[0].Id);
        CanAddJointPart(builder, kind, beam, out var offJoint).ShouldBeFalse();
        offJoint.ShouldBe(JointPartReasons(kind).OffJoint);
        foreach (var neighbour in new[] { CreatureElementKind.Wheel, CreatureElementKind.TouchSensor, CreatureElementKind.Rubber }.Where(neighbour => neighbour != kind))
        {
            AddJointPart(builder, neighbour, joint);
        }

        builder.AddServo(joint);
        var expectedId = builder.NextPartId;

        var part = AddJointPart(builder, kind, joint);

        part.ShouldBe(expectedId);
        CanAddJointPart(builder, kind, joint, out var full).ShouldBeFalse();
        full.ShouldBe(JointPartReasons(kind).Full);
        Should.Throw<ArgumentException>(() => AddJointPart(builder, kind, joint));
        var kept = AddJointPart(builder, kind, other);
        builder.NodeRadius(other).ShouldBe(kind == CreatureElementKind.Wheel ? WheelDef.DefaultRadius : NodeDef.PlainJointRadius);
        builder.Rename(kept, "Toe");
        new CreatureBuilder(builder.Build()).JointParts.ShouldBe(builder.JointParts);
        builder.RemoveNode(joint);
        builder.JointParts.Select(jointPart => (jointPart.Id, jointPart.Name)).ShouldBe([(kept, "Toe")]);
        builder.RemovePart(kept);
        builder.JointParts.ShouldBeEmpty();
        builder.Nodes.Select(node => node.Id).ShouldBe([other]);
    }

    // The settings panel lists each part's settings in this order, and setting one leaves the rest.
    [Fact]
    public void EveryPartsSettings_ComeInPanelOrder_AndSettingOneKeepsTheOthers()
    {
        var parts = EveryPart();
        var builder = parts.Builder;
        var expected = new Dictionary<int, PartParameterId[]>
        {
            [parts.Piston] = [PartParameterId.Strength, PartParameterId.Stroke, PartParameterId.StartPosition, PartParameterId.MaxSpeed, PartParameterId.RiseTime],
            [parts.Servo] = [PartParameterId.ServoStrength, PartParameterId.Range, PartParameterId.ServoStartPosition, PartParameterId.AngularMaxSpeed, PartParameterId.RiseTime],
            [parts.Motor] = [PartParameterId.MotorStrength, PartParameterId.MotorMaxSpeed, PartParameterId.RiseTime],
            [parts.Stepper] = [PartParameterId.ServoStrength, PartParameterId.StepsPerTurn, PartParameterId.MaxFrequency],
            [parts.Brake] = [PartParameterId.BrakeStrength],
            [parts.Spring] = [PartParameterId.Stiffness, PartParameterId.Damping, PartParameterId.Stroke, PartParameterId.CoilLength],
            [parts.Wheel] = [PartParameterId.WheelRadius],
            [parts.Camera] = [PartParameterId.Aim, PartParameterId.Rays, PartParameterId.Spread, PartParameterId.CameraRange],
        };

        builder.PartIdsWithSettings.ShouldBe(expected.Keys, ignoreOrder: true);
        builder.Parts.Where(part => !expected.ContainsKey(part.Id)).ShouldAllBe(part => builder.ParametersOf(part.Id).Count == 0);
        Should.Throw<ArgumentOutOfRangeException>(() => builder.SetParameter(parts.Accelerometer, PartParameterId.Aim, 1));
        foreach (var (part, parameters) in expected)
        {
            builder.ParametersOf(part).ShouldBe(parameters);
            var notItsOwn = Enum.GetValues<PartParameterId>().First(parameter => !parameters.Contains(parameter));
            Should.Throw<ArgumentOutOfRangeException>(() => builder.SetParameter(part, notItsOwn, 1));
            Should.Throw<ArgumentOutOfRangeException>(() => builder.ParameterValue(part, notItsOwn));
            foreach (var parameter in parameters)
            {
                var others = parameters.Where(other => other != parameter).ToDictionary(other => other, other => builder.ParameterValue(part, other));
                var value = parameter == PartParameterId.Aim ? 0.3 : PartParameters.ValueAt(parameter, 0.25);

                builder.SetParameter(part, parameter, value);

                builder.ParameterValue(part, parameter).ShouldBe(value, 1e-9, $"{part} {parameter}");
                foreach (var (other, before) in others)
                {
                    builder.ParameterValue(part, other).ShouldBe(before, 1e-9, $"{part} {other} after {parameter}");
                }
            }
        }

        builder.IsWithinBuildLimits().ShouldBeTrue();
        builder.NodeRadius(builder.Wheels.Single().NodeId).ShouldBe(builder.Wheels.Single().Radius);
    }

    [Fact]
    public void WholeNumberSettings_RoundOrRefuse_AndASingleRayCameraHasNoSpread()
    {
        var parts = EveryPart();
        var builder = parts.Builder;

        builder.SetParameter(parts.Stepper, PartParameterId.StepsPerTurn, 11.6);
        builder.Steppers.Single().StepsPerTurn.ShouldBe(12);
        Should.Throw<ArgumentOutOfRangeException>(() => builder.SetParameter(parts.Stepper, PartParameterId.StepsPerTurn, 25));
        Should.Throw<ArgumentOutOfRangeException>(() => builder.SetParameter(parts.Camera, PartParameterId.Rays, 2));
        Should.Throw<ArgumentOutOfRangeException>(() => builder.SetParameter(parts.Camera, PartParameterId.Rays, 2.5));
        builder.Steppers.Single().StepsPerTurn.ShouldBe(12);
        builder.Sensors.Single(sensor => sensor.Id == parts.Camera).Rays.ShouldBe(3);
        builder.ParameterHasEffect(parts.Camera, PartParameterId.Spread).ShouldBeTrue();
        builder.SetParameter(parts.Camera, PartParameterId.Rays, 1);
        builder.ParameterHasEffect(parts.Camera, PartParameterId.Spread).ShouldBeFalse();
        builder.ParameterHasEffect(parts.Camera, PartParameterId.CameraRange).ShouldBeTrue();
    }

    // A creature from outside the app, like a share code, must be one Build could make: each
    // setting on its slider, a Brake's Locked top step included.
    [Theory]
    [InlineData(BrakeDef.LockedStrength, true)]
    [InlineData(199999.0, false)]
    public void IsWithinBuildLimits_TakesEachSettingOnItsSlider(double brakeStrength, bool within)
    {
        var parts = EveryPart();

        parts.Builder.SetParameter(parts.Brake, PartParameterId.BrakeStrength, brakeStrength);

        parts.Builder.IsWithinBuildLimits().ShouldBe(within);
    }

    [Fact]
    public void ACreature_RoundTripsThroughTheBuilder_AndRemovingAJointTakesEverythingOnIt()
    {
        var built = EveryPart().Builder.Build();

        var builder = new CreatureBuilder(built);

        builder.Build().Parts.ShouldBe(built.Parts);
        builder.Build().NextPartId.ShouldBe(built.NextPartId);
        foreach (var part in builder.Parts)
        {
            builder.Rename(part.Id, $"Part {part.Id}");
        }

        builder.Parts.ShouldAllBe(part => part.Name == $"Part {part.Id}");
        Should.Throw<ArgumentOutOfRangeException>(() => builder.Rename(999, "Ghost"));
        Should.Throw<ArgumentOutOfRangeException>(() => builder.Rename(0, "Ghost"));
        var middle = builder.Nodes[1];
        builder.MoveNode(middle.Id, new Vector2D(130, 10));
        builder.Nodes[1].ShouldBe(new NodeDef(middle.Id, new Vector2D(130, 10), middle.Name));
        builder.TryBuild(out _, out _).ShouldBeTrue();
        var survivors = builder.Parts.Where(part => part.Id is 3 or 9 or 12).ToList();

        builder.RemoveNode(middle.Id);

        builder.Nodes.Select(node => node.Id).ShouldNotContain(middle.Id);
        builder.Beams.ShouldAllBe(beam => beam.NodeA != middle.Id && beam.NodeB != middle.Id);
        builder.Sensors.ShouldBeEmpty();
        builder.VelocityMotors.ShouldBeEmpty();
        builder.Servos.Single().FixedLinkId.ShouldBeNull();
        builder.Parts.Where(part => part.Id is 3 or 9 or 12).ShouldBe(survivors);
        builder.TryBuild(out var creature, out var problems).ShouldBeFalse();
        creature.ShouldBeNull();
        problems.ShouldNotBeEmpty();
        builder.AddNode(new Vector2D(0, -90)).ShouldBe(built.NextPartId);
    }

    public static TheoryData<PartKind> EveryKind => [.. PartKinds.All.Select(info => info.Kind)];

    // Every kind is found, renamed and removed by its id alone; a removal takes what goes with the part
    // and leaves each hinge part that used it as a side without that side.
    [Theory]
    [MemberData(nameof(EveryKind))]
    public void EveryPartKind_IsFoundRenamedAndRemovedById(PartKind kind)
    {
        var builder = EveryPart().Builder;
        var before = builder.Build();
        var part = before.Parts.First(part => part.PartKind == kind);

        builder.FindPart(part.Id).ShouldBe(part);

        builder.Rename(part.Id, "Renamed");
        builder.FindPart(part.Id).ShouldBe(part.WithName("Renamed"));
        builder.Build().Parts.Where(other => other.Id != part.Id).ShouldBe(before.Parts.Where(other => other.Id != part.Id));

        builder.RemovePart(part.Id);
        var gone = GoesWith(before, part).Append(part.Id).ToHashSet();
        var after = builder.Build();
        after.Parts.Select(kept => kept.Id).ShouldBe(before.Parts.Select(kept => kept.Id).Where(id => !gone.Contains(id)));
        after.HingeParts.ShouldBe(before.HingeParts
            .Where(hinge => !gone.Contains(hinge.Id))
            .Select(hinge => hinge.WithLinks(Kept(hinge.FixedLinkId), Kept(hinge.TargetLinkId))));
        builder.FindPart(part.Id).ShouldBeNull();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.RemovePart(part.Id));
        Should.Throw<ArgumentOutOfRangeException>(() => builder.Rename(part.Id, "Ghost"));

        int? Kept(int? side) => side is { } id && gone.Contains(id) ? null : side;
    }

    // Every hinge part changes a side by its id alone, as each kind's own method does, and keeps its id.
    [Fact]
    public void EveryHingePart_ChangesASideById()
    {
        var builder = EveryPart().Builder;
        var hingeKinds = PartKinds.All.Where(info => info.Slot == JointSlot.HingePart).Select(info => info.Kind).ToArray();

        foreach (var part in hingeKinds.Select(kind => builder.HingeParts.Single(part => part.PartKind == kind)))
        {
            builder.SetHingePartLink(part.Id, fixedRole: true, part.TargetLinkId!.Value);
            ShouldHaveSides(builder, part.Id, part.TargetLinkId, part.FixedLinkId);
        }

        builder.HingeParts.Select(part => part.PartKind).ShouldBe(hingeKinds, ignoreOrder: true);
        Should.Throw<ArgumentOutOfRangeException>(() => builder.SetHingePartLink(builder.Wheels[0].Id, fixedRole: true, builder.Beams[0].Id));
        Should.Throw<ArgumentOutOfRangeException>(() => builder.SetHingePartLink(999, fixedRole: true, builder.Beams[0].Id));
    }

    [Fact]
    public void AWorm_Builds()
    {
        var builder = new CreatureBuilder();
        var previous = builder.AddNode(new Vector2D(0, 0));
        previous.ShouldBe(1);
        for (var i = 1; i < 5; i++)
        {
            var next = builder.AddNode(new Vector2D(i * 90, 0));
            builder.AddBeam(previous, next);
            previous = next;
        }

        builder.AddSensor(builder.Beams[0].Id, SensorKind.Accelerometer, out _, out _).ShouldBeTrue();

        builder.TryBuild(out var creature, out var errors).ShouldBeTrue();
        errors.ShouldBeEmpty();
        creature!.Parts.ShouldBe(builder.Build().Parts);
    }

    [Fact]
    public void ALink_OnAPairAlreadyJoined_OrOnABeamWithAPart_IsRefusedWithWhy()
    {
        var builder = PairBuilder();
        var (a, b) = (builder.Nodes[0].Id, builder.Nodes[1].Id);
        var c = builder.AddNode(new Vector2D(0, 90));
        var d = builder.AddNode(new Vector2D(90, 90));
        builder.AddSpring(a, c);
        builder.AddPiston(b, d);
        builder.AddSensor(builder.Beams[0].Id, SensorKind.Accelerometer, out _, out _);

        builder.CanAddBeam(b, a, out var onBeam).ShouldBeFalse();
        onBeam.ShouldBe(CreatureBuilder.BeamJoinsTheseNodesReason);
        builder.CanAddSpring(c, a, out var onSpring).ShouldBeFalse();
        onSpring.ShouldBe(CreatureBuilder.SpringJoinsTheseNodesReason);
        builder.CanAddPiston(d, b, out var onPiston).ShouldBeFalse();
        onPiston.ShouldBe(CreatureBuilder.PistonJoinsTheseNodesReason);
        builder.CanAddPiston(a, b, out var pistonOnSensor).ShouldBeFalse();
        pistonOnSensor.ShouldBe(CreatureBuilder.PartSitsOnThisBeamReason);
        builder.CanAddSpring(b, a, out var springOnSensor).ShouldBeFalse();
        springOnSensor.ShouldBe(CreatureBuilder.PartSitsOnThisBeamReason);
        builder.CanAddSpring(a, a, out _).ShouldBeFalse();
        builder.CanAddSpring(c, d, out _).ShouldBeTrue();
        Should.Throw<ArgumentException>(() => builder.AddBeam(b, a));
        Should.Throw<ArgumentException>(() => builder.AddSpring(a, c));
        Should.Throw<ArgumentException>(() => builder.AddPiston(a, b));
        Should.Throw<ArgumentOutOfRangeException>(() => builder.RemovePart(999));
        builder.Parts.Count.ShouldBe(8);

        var e = builder.AddNode(new Vector2D(180, 0));
        builder.AddMemoryCell(builder.AddBeam(b, e));
        builder.CanAddPiston(b, e, out var pistonOnCell).ShouldBeFalse();
        pistonOnCell.ShouldBe(CreatureBuilder.PartSitsOnThisBeamReason);
    }

    // Any link replaces a link of another kind on its pair, with its default settings, and a
    // hinge part holding the old one holds the new one in the same role under the same id.
    [Theory]
    [InlineData(BuildLink.Beam, BuildLink.Piston, true)]
    [InlineData(BuildLink.Piston, BuildLink.Spring, false)]
    [InlineData(BuildLink.Spring, BuildLink.Beam, true)]
    public void ALink_OnAPairAnotherKindJoins_ReplacesIt_AndTakesItsServoRole(BuildLink old, BuildLink drawn, bool replaceFixed)
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0));
        var b = builder.AddNode(new Vector2D(90, 0));
        var c = builder.AddNode(new Vector2D(0, 90));
        var fixedLink = AddLink(builder, replaceFixed ? old : BuildLink.Beam, a, b);
        var targetLink = AddLink(builder, replaceFixed ? BuildLink.Beam : old, a, c);
        var servo = builder.AddServo(a, fixedLink, targetLink);
        var (from, to) = replaceFixed ? (b, a) : (c, a);

        var link = AddLink(builder, drawn, from, to);

        LinkIds(builder).ShouldBe(replaceFixed ? [targetLink, link] : [fixedLink, link], ignoreOrder: true);
        builder.LinkBetween(to, from).ShouldBe(link);
        builder.Servos.Single().ShouldBe(new ServoDef(servo, a, replaceFixed ? link : fixedLink, replaceFixed ? targetLink : link));
        builder.NextPartId.ShouldBe(link + 1);
        builder.Pistons.ShouldBe(drawn == BuildLink.Piston ? [new PistonDef(link, from, to)] : []);
        builder.Springs.ShouldBe(drawn == BuildLink.Spring ? [new SpringDef(link, from, to)] : []);
    }

    // One sensor per beam; a moved sensor keeps its id, kind, name and settings.
    [Fact]
    public void Sensors_SitOnePerBeam_AndMoveKeepingTheirIdNameAndSettings()
    {
        var builder = TwoBeams(out var level, out var down);
        var third = builder.AddBeam(builder.Nodes[0].Id, builder.Nodes[2].Id);
        builder.AddSensor(level, SensorKind.Accelerometer, out var tilt, out _).ShouldBeTrue();
        builder.Rename(tilt, "Tilt");

        builder.AddSensor(level, SensorKind.Camera, out var refusedId, out var refused).ShouldBeFalse();
        builder.AddSensor(down, SensorKind.Camera, out var camera, out _).ShouldBeTrue();
        builder.MoveSensor(tilt, down, out var taken).ShouldBeFalse();
        builder.MoveSensor(tilt, third, out var none).ShouldBeTrue();

        refusedId.ShouldBe(0);
        refused.ShouldBe(CreatureBuilder.OnePartPerBeamReason);
        taken.ShouldBe(CreatureBuilder.OnePartPerBeamReason);
        none.ShouldBeNull();
        builder.Sensors.ShouldBe(
        [
            new SensorDef(tilt, third, SensorKind.Accelerometer, "Tilt"),
            new SensorDef(camera, down, SensorKind.Camera, aim: SensorDef.DefaultAim(new Vector2D(90, 0), new Vector2D(90, 90))),
        ]);
        var before = builder.Sensors.ToList();
        builder.MoveSensor(camera, down, out _).ShouldBeTrue();
        builder.Sensors.ShouldBe(before);
        builder.RemovePart(tilt);
        builder.RemovePart(down);
        builder.Sensors.ShouldBeEmpty();
    }

    // A Camera keeps the world direction it looks in as built: level → down turns the beam by
    // +π/2 and down → level by −π/2.
    [Theory]
    [InlineData(false, 0.3 - (Math.PI / 2))]
    [InlineData(true, 0.3 + (Math.PI / 2))]
    public void MoveSensor_Camera_KeepsTheWorldDirectionItLooksIn_AndItsSettings(bool fromTheDownBeam, double expectedAim)
    {
        var builder = TwoBeams(out var level, out var down);
        var (from, to) = fromTheDownBeam ? (down, level) : (level, down);
        builder.AddSensor(from, SensorKind.Camera, out var camera, out _);
        builder.SetParameter(camera, PartParameterId.Aim, 0.3);
        builder.SetParameter(camera, PartParameterId.Rays, 5);
        builder.SetParameter(camera, PartParameterId.Spread, 0.5);
        builder.SetParameter(camera, PartParameterId.CameraRange, 300);

        builder.MoveSensor(camera, to, out _).ShouldBeTrue();

        var moved = builder.Sensors.Single();
        moved.Aim!.Value.ShouldBe(expectedAim, 1e-9);
        moved.ShouldBe(new SensorDef(camera, to, SensorKind.Camera, aim: moved.Aim, rays: 5, spread: 0.5, range: 300));
    }

    // Nodes 1 (0,0), 2 (120,0), 3 (240,0), 4 (0,120), 5 (120,120), 6 (240,120); beams 7 (1–2), 8 (2–3),
    // 9 (1–4), 10 (2–5), 11 (3–6); every other part on them, a Memory cell on beam 9.
    private static EveryPartIds EveryPart()
    {
        var builder = new CreatureBuilder();
        var n = new[] { (0, 0), (120, 0), (240, 0), (0, 120), (120, 120), (240, 120) }.Select(p => builder.AddNode(new Vector2D(p.Item1, p.Item2))).ToArray();
        var b12 = builder.AddBeam(n[0], n[1]);
        var b23 = builder.AddBeam(n[1], n[2]);
        var b14 = builder.AddBeam(n[0], n[3]);
        builder.AddBeam(n[1], n[4]);
        builder.AddBeam(n[2], n[5]);
        var piston = builder.AddPiston(n[3], n[4]);
        var spring = builder.AddSpring(n[4], n[5]);
        builder.AddSensor(b12, SensorKind.Accelerometer, out var accelerometer, out _);
        builder.AddSensor(b23, SensorKind.Camera, out var camera, out _);
        builder.AddMemoryCell(b14);
        return new EveryPartIds(
            builder,
            piston,
            spring,
            Servo: builder.AddServo(n[0]),
            Motor: builder.AddVelocityMotor(n[1]),
            Stepper: builder.AddStepper(n[2]),
            Brake: builder.AddBrake(n[3]),
            AngleSensor: builder.AddAngleSensor(n[4]),
            Wheel: builder.AddWheel(n[5]),
            Touch: builder.AddTouchSensor(n[5]),
            Rubber: builder.AddRubber(n[5]),
            accelerometer,
            camera);
    }

    private sealed record EveryPartIds(
        CreatureBuilder Builder,
        int Piston,
        int Spring,
        int Servo,
        int Motor,
        int Stepper,
        int Brake,
        int AngleSensor,
        int Wheel,
        int Touch,
        int Rubber,
        int Accelerometer,
        int Camera);

    // What removing part takes with it, worked out from the creature: a joint takes its links, the parts
    // on it and the sensor or Memory cell on each of its beams; a beam takes its own.
    private static IEnumerable<int> GoesWith(CreatureDef creature, IPartDef part)
    {
        if (part is NodeDef node)
        {
            var beams = creature.Beams.Where(beam => beam.NodeA == node.Id || beam.NodeB == node.Id).Select(beam => beam.Id).ToArray();
            return [
                .. beams,
                .. creature.Pistons.Where(piston => piston.NodeA == node.Id || piston.NodeB == node.Id).Select(piston => piston.Id),
                .. creature.Springs.Where(spring => spring.NodeA == node.Id || spring.NodeB == node.Id).Select(spring => spring.Id),
                .. CreatureDef.JointPartDefs(node.Id, creature.Parts).Select(jointPart => jointPart.Id),
                .. creature.BeamParts.Where(beamPart => beams.Contains(beamPart.BeamId)).Select(beamPart => beamPart.Id),
            ];
        }

        return creature.BeamParts.Where(beamPart => beamPart.BeamId == part.Id).Select(beamPart => beamPart.Id);
    }

    private static void ShouldHaveSides(CreatureBuilder builder, int partId, int? fixedLink, int? targetLink)
    {
        var part = builder.HingeParts.Single(part => part.Id == partId);
        part.FixedLinkId.ShouldBe(fixedLink);
        part.TargetLinkId.ShouldBe(targetLink);
    }

    private static UiText OffJointReason(CreatureElementKind kind) => kind switch
    {
        CreatureElementKind.Servo => CreatureBuilder.ServosGoOnAJointReason,
        CreatureElementKind.VelocityMotor => CreatureBuilder.VelocityMotorsGoOnAJointReason,
        CreatureElementKind.Stepper => CreatureBuilder.SteppersGoOnAJointReason,
        CreatureElementKind.Brake => CreatureBuilder.BrakesGoOnAJointReason,
        _ => CreatureBuilder.AngleSensorsGoOnAJointReason,
    };

    private static bool CanAddHingePart(CreatureBuilder builder, CreatureElementKind kind, int nodeId, out UiText? reason)
    {
        UiText? why = null;
        var can = kind switch
        {
            CreatureElementKind.Servo => builder.CanAddServo(nodeId, out why),
            CreatureElementKind.VelocityMotor => builder.CanAddVelocityMotor(nodeId, out why),
            CreatureElementKind.Stepper => builder.CanAddStepper(nodeId, out why),
            CreatureElementKind.Brake => builder.CanAddBrake(nodeId, out why),
            _ => builder.CanAddAngleSensor(nodeId, out why),
        };
        reason = why;
        return can;
    }

    private static int AddHingePart(CreatureBuilder builder, CreatureElementKind kind, int nodeId) => kind switch
    {
        CreatureElementKind.Servo => builder.AddServo(nodeId),
        CreatureElementKind.VelocityMotor => builder.AddVelocityMotor(nodeId),
        CreatureElementKind.Stepper => builder.AddStepper(nodeId),
        CreatureElementKind.Brake => builder.AddBrake(nodeId),
        _ => builder.AddAngleSensor(nodeId),
    };

    private static int AddHingePart(CreatureBuilder builder, CreatureElementKind kind, int nodeId, int? fixedLink, int? targetLink) => kind switch
    {
        CreatureElementKind.Servo => builder.AddServo(nodeId, fixedLink, targetLink),
        CreatureElementKind.VelocityMotor => builder.AddVelocityMotor(nodeId, fixedLink, targetLink),
        CreatureElementKind.Stepper => builder.AddStepper(nodeId, fixedLink, targetLink),
        CreatureElementKind.Brake => builder.AddBrake(nodeId, fixedLink, targetLink),
        _ => builder.AddAngleSensor(nodeId, fixedLink, targetLink),
    };

    private static (UiText OffJoint, UiText Full) JointPartReasons(CreatureElementKind kind) => kind switch
    {
        CreatureElementKind.Wheel => (CreatureBuilder.WheelsGoOnAJointReason, CreatureBuilder.OneWheelPerJointReason),
        CreatureElementKind.TouchSensor => (CreatureBuilder.TouchSensorsGoOnAJointReason, CreatureBuilder.OneSensorPerJointReason),
        _ => (CreatureBuilder.RubberGoesOnAJointReason, CreatureBuilder.OneRubberPerJointReason),
    };

    private static bool CanAddJointPart(CreatureBuilder builder, CreatureElementKind kind, int nodeId, out UiText? reason)
    {
        UiText? why = null;
        var can = kind switch
        {
            CreatureElementKind.Wheel => builder.CanAddWheel(nodeId, out why),
            CreatureElementKind.TouchSensor => builder.CanAddTouchSensor(nodeId, out why),
            _ => builder.CanAddRubber(nodeId, out why),
        };
        reason = why;
        return can;
    }

    private static int AddJointPart(CreatureBuilder builder, CreatureElementKind kind, int nodeId) => kind switch
    {
        CreatureElementKind.Wheel => builder.AddWheel(nodeId),
        CreatureElementKind.TouchSensor => builder.AddTouchSensor(nodeId),
        _ => builder.AddRubber(nodeId),
    };

    private static int AddLink(CreatureBuilder builder, BuildLink link, int nodeIdA, int nodeIdB) => link switch
    {
        BuildLink.Beam => builder.AddBeam(nodeIdA, nodeIdB),
        BuildLink.Piston => builder.AddPiston(nodeIdA, nodeIdB),
        _ => builder.AddSpring(nodeIdA, nodeIdB),
    };

    private static IEnumerable<int> LinkIds(CreatureBuilder builder) =>
        [.. builder.Beams.Select(beam => beam.Id), .. builder.Pistons.Select(piston => piston.Id), .. builder.Springs.Select(spring => spring.Id)];

    // A joint with two beams, to far and to a third node.
    private static CreatureBuilder Joint(out int joint, out int far, out int first, out int second)
    {
        var builder = new CreatureBuilder();
        joint = builder.AddNode(new Vector2D(0, 0));
        far = builder.AddNode(new Vector2D(120, 0));
        first = builder.AddBeam(joint, far);
        second = builder.AddBeam(joint, builder.AddNode(new Vector2D(0, 90)));
        return builder;
    }

    private static CreatureBuilder PairBuilder()
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0));
        var b = builder.AddNode(new Vector2D(90, 0));
        builder.AddBeam(a, b);
        return builder;
    }

    // A level beam from (0,0) to (90,0), and one from (90,0) straight down to (90,90).
    private static CreatureBuilder TwoBeams(out int level, out int down)
    {
        var builder = PairBuilder();
        level = builder.Beams[0].Id;
        down = builder.AddBeam(builder.Beams[0].NodeB, builder.AddNode(new Vector2D(90, 90)));
        return builder;
    }
}
