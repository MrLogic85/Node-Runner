using System.Text.Json;

namespace NodeRunner.Domain.Tests;

public sealed class StepperDefTests
{
    private static readonly NodeDef[] _nodes = [new(1, new Vector2D(0, 0)), new(2, new Vector2D(100, 0)), new(3, new Vector2D(0, 100))];

    private static readonly BeamDef[] _beams = [new(4, 1, 2), new(5, 1, 3)];

    // #1036: generation 0 runs it at half its Max frequency, so 2 Hz starts it at a step a second.
    [Fact]
    public void Constructor_HasNoNameOrSides_AndTheDesignedSettings_ByDefault()
    {
        var stepper = new StepperDef(6, 1);

        (stepper.Name, stepper.FixedLinkId, stepper.TargetLinkId).ShouldBe((null, null, null));
        (stepper.Strength, stepper.StepsPerTurn, stepper.MaxFrequency).ShouldBe((1500000, 8, 2));
    }

    [Theory]
    [InlineData(0, 1, null, null)]
    [InlineData(6, 0, null, null)]
    [InlineData(6, 1, 0, null)]
    [InlineData(6, 1, null, -4)]
    public void Constructor_WithoutPositiveIds_Throws(int id, int nodeId, int? fixedLinkId, int? targetLinkId)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new StepperDef(id, nodeId, fixedLinkId, targetLinkId));
    }

    [Fact]
    public void Constructor_WithTheSameSideTwice_Throws()
    {
        Should.Throw<ArgumentException>(() => new StepperDef(6, 1, 4, 4));
    }

    [Theory]
    [InlineData(0, 8, 2)]
    [InlineData(double.PositiveInfinity, 8, 2)]
    [InlineData(double.NaN, 8, 2)]
    [InlineData(1, 1, 2)]
    [InlineData(1, 25, 2)]
    [InlineData(1, 8, 0)]
    [InlineData(1, 8, double.PositiveInfinity)]
    public void Constructor_WithASettingOffItsRange_Throws(double strength, int stepsPerTurn, double maxFrequency)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new StepperDef(6, 1, strength: strength, stepsPerTurn: stepsPerTurn, maxFrequency: maxFrequency));
    }

    [Theory]
    [InlineData(StepperDef.MinStepsPerTurn)]
    [InlineData(StepperDef.MaxStepsPerTurn)]
    public void Constructor_AcceptsTheEndsOfStepsPerTurn(int stepsPerTurn)
    {
        new StepperDef(6, 1, stepsPerTurn: stepsPerTurn).StepsPerTurn.ShouldBe(stepsPerTurn);
    }

    [Fact]
    public void WithName_WithSettings_AndWithLinks_KeepItsIdAndTheRest()
    {
        var stepper = new StepperDef(6, 1, 4, 5, "Ratchet", 300000, 12, 1.5);

        stepper.WithName("Clock").ShouldBe(new StepperDef(6, 1, 4, 5, "Clock", 300000, 12, 1.5));
        stepper.WithSettings(100000, 4, 3).ShouldBe(new StepperDef(6, 1, 4, 5, "Ratchet", 100000, 4, 3));
        stepper.WithLinks(5, null).ShouldBe(new StepperDef(6, 1, 5, null, "Ratchet", 300000, 12, 1.5));
    }

    [Fact]
    public void CreatureDef_AcceptsMissingSidesAndItsJointsWheel_ButNotAnotherJointsLinksOrWheel()
    {
        WheelDef[] wheels = [new(7, 1), new(8, 2)];

        var creature = Creature([new StepperDef(6, 1, null, 7)], wheels: wheels);

        creature.Steppers.ShouldBe([new StepperDef(6, 1, null, 7)]);
        creature.StepperIndexOf(6).ShouldBe(0);
        Should.Throw<ArgumentOutOfRangeException>(() => creature.StepperIndexOf(7));
        Should.Throw<ArgumentException>(() => Creature([new StepperDef(6, 1, 4, 8)], wheels: wheels));
        Should.Throw<ArgumentException>(() => Creature([new StepperDef(6, 2, 4, 5)]));
        Should.Throw<ArgumentException>(() => Creature([new StepperDef(6, 9, 4, 5)]));
        Should.Throw<ArgumentException>(() => Creature([new StepperDef(6, 1, 4, 99)]));
    }

    // A joint holds one Servo, Motor, Brake, Angle sensor or Stepper (#1044).
    [Fact]
    public void CreatureDef_SharesTheJointsHingePartSlot_WithEveryOtherHingePart()
    {
        StepperDef[] stepper = [new StepperDef(6, 1, 4, 5)];

        Should.Throw<ArgumentException>(() => Creature(stepper, servos: [new ServoDef(7, 1, 5, 4)])).Message.ShouldContain("hinge part");
        Should.Throw<ArgumentException>(() => Creature(stepper, motors: [new VelocityMotorDef(7, 1, 5, 4)])).Message.ShouldContain("hinge part");
        Should.Throw<ArgumentException>(() => Creature(stepper, brakes: [new BrakeDef(7, 1, 5, 4)])).Message.ShouldContain("hinge part");
        Should.Throw<ArgumentException>(() => Creature(stepper, angleSensors: [new AngleSensorDef(7, 1, 5, 4)])).Message.ShouldContain("hinge part");
        Should.Throw<ArgumentException>(() => Creature([.. stepper, new StepperDef(7, 1, 5, 4)])).Message.ShouldContain("hinge part");
    }

    // It is a joint motor: it turns its joint, so the joint takes a motor's radius.
    [Fact]
    public void CreatureDef_CountsAStepperAsAPartAndAJointMotor_AfterTheServosAndMotors()
    {
        var without = Creature([]);
        var with = Creature([new StepperDef(6, 1, 4, 5)], servos: [new ServoDef(7, 2, 4)], motors: [new VelocityMotorDef(8, 3, 5)]);

        with.PartCount.ShouldBe(without.PartCount + 3);
        with.JointMotors.Select(motor => motor.Id).ShouldBe([7, 8, 6]);
        with.HingeParts.Select(part => (part.Kind, part.Id)).ShouldBe(
            [(CreatureElementKind.Servo, 7), (CreatureElementKind.VelocityMotor, 8), (CreatureElementKind.Stepper, 6)]);
        with.NodeRadius(1).ShouldBe(with.NodeRadius(2));
        with.NodeRadius(1).ShouldBeGreaterThan(without.NodeRadius(1));
    }

    [Fact]
    public void HasJointPart_CountsAStepper()
    {
        CreatureDef.HasJointPart(1, [new StepperDef(2, 1)]).ShouldBeTrue();
        CreatureDef.HasJointPart(3, [new StepperDef(2, 1)]).ShouldBeFalse();
    }

    [Fact]
    public void JsonRoundTrip_PreservesSteppers()
    {
        var original = Creature([new StepperDef(6, 1, 4, 7, "Ratchet", 300000, 12, 1.5), new StepperDef(8, 2, null, 4)], wheels: [new WheelDef(7, 1)]);

        var roundTripped = JsonSerializer.Deserialize<CreatureDef>(JsonSerializer.Serialize(original));

        roundTripped.ShouldNotBeNull();
        roundTripped.Steppers.ShouldBe(original.Steppers);
    }

    private static CreatureDef Creature(
        StepperDef[] steppers,
        ServoDef[]? servos = null,
        VelocityMotorDef[]? motors = null,
        BrakeDef[]? brakes = null,
        AngleSensorDef[]? angleSensors = null,
        WheelDef[]? wheels = null) =>
        new(_nodes, _beams, [], servos ?? [], [], [], wheels ?? [], [], [], motors ?? [], brakes ?? [], angleSensors ?? [], steppers, nextPartId: 10);
}
