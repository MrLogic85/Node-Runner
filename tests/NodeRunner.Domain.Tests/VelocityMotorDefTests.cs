namespace NodeRunner.Domain.Tests;

public sealed class VelocityMotorDefTests
{
    private static readonly NodeDef[] _nodes = [new(1, new Vector2D(0, 0)), new(2, new Vector2D(100, 0)), new(3, new Vector2D(0, 100))];

    private static readonly BeamDef[] _beams = [new(4, 1, 2), new(5, 1, 3)];

    [Fact]
    public void Constructor_ValidatesItsSettings_AndDefaultsToTheServosButWeaker()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new VelocityMotorDef(0, 1, 2, 3));
        Should.Throw<ArgumentOutOfRangeException>(() => new VelocityMotorDef(1, 0, 2, 3));
        Should.Throw<ArgumentOutOfRangeException>(() => new VelocityMotorDef(1, 1, 0, 3));
        Should.Throw<ArgumentOutOfRangeException>(() => new VelocityMotorDef(1, 1, 2, 3, strength: 0));
        Should.Throw<ArgumentOutOfRangeException>(() => new VelocityMotorDef(1, 1, 2, 3, maxSpeed: double.NaN));
        Should.Throw<ArgumentOutOfRangeException>(() => new VelocityMotorDef(1, 1, 2, 3, riseTime: 0));
        Should.Throw<ArgumentException>(() => new VelocityMotorDef(1, 1, 2, 2));

        var motor = new VelocityMotorDef(1, 1);
        motor.Strength.ShouldBe(400000);
        motor.MaxSpeed.ShouldBe(ServoDef.DefaultMaxSpeed);
        motor.RiseTime.ShouldBe(ServoDef.DefaultRiseTime);
    }

    [Fact]
    public void WithLinks_KeepsItsIdAndSettings()
    {
        var motor = new VelocityMotorDef(6, 1, 4, 5, "Drive", strength: 1000, maxSpeed: 3, riseTime: 0.5);

        motor.WithLinks(5, null).ShouldBe(new VelocityMotorDef(6, 1, 5, null, "Drive", strength: 1000, maxSpeed: 3, riseTime: 0.5));
    }

    [Fact]
    public void CreatureDef_AcceptsMissingSidesAndItsJointsWheel_ButNotAnotherJointsLinks()
    {
        WheelDef[] wheels = [new(7, 1), new(8, 2)];

        var creature = Creature([new VelocityMotorDef(6, 1, null, 7)], wheels: wheels);

        creature.VelocityMotors.Single().FixedLinkId.ShouldBeNull();
        creature.VelocityMotorIndexOf(6).ShouldBe(0);
        Should.Throw<ArgumentException>(() => Creature([new VelocityMotorDef(6, 1, 4, 8)], wheels: wheels));
        Should.Throw<ArgumentException>(() => Creature([new VelocityMotorDef(6, 2, 4, 5)]));
        Should.Throw<ArgumentException>(() => Creature([new VelocityMotorDef(6, 1, 4, 5), new VelocityMotorDef(7, 1, 4, 5)]));
    }

    [Fact]
    public void CreatureDef_ShareTheJointsMotorSlot_WithAServo()
    {
        var error = Should.Throw<ArgumentException>(() => Creature([new VelocityMotorDef(6, 1, 4, 5)], servos: [new ServoDef(7, 1, 5, 4)]));

        error.Message.ShouldContain("hinge part");
    }

    [Fact]
    public void CreatureDef_CountsAVelocityMotorAsAJointMotor()
    {
        var creature = Creature([new VelocityMotorDef(7, 1, 4, 5)], servos: [new ServoDef(6, 2, 4)]);

        creature.JointMotors.Select(motor => (motor.Kind, motor.Id)).ShouldBe([(CreatureElementKind.Servo, 6), (CreatureElementKind.VelocityMotor, 7)]);
        creature.PartCount.ShouldBe(7);
        creature.NodeRadius(1).ShouldBe(JointMotor.Radius);
        creature.CoveredRadius(1).ShouldBe(JointMotor.Radius);
        creature.JointPartsAt(1).ShouldBe([new CreatureElementSelection(CreatureElementKind.VelocityMotor, 7)]);
        CreatureDef.HasJointPart(1, creature.Parts).ShouldBeTrue();
    }

    private static CreatureDef Creature(VelocityMotorDef[] motors, ServoDef[]? servos = null, WheelDef[]? wheels = null) =>
        new(_nodes, _beams, [], servos ?? [], [], [], wheels ?? [], [], [], motors, [], nextPartId: 10);
}
