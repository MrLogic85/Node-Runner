using System.Text.Json;

namespace NodeRunner.Domain.Tests;

public sealed class BrakeDefTests
{
    private static readonly NodeDef[] _nodes = [new(1, new Vector2D(0, 0)), new(2, new Vector2D(100, 0)), new(3, new Vector2D(0, 100))];

    private static readonly BeamDef[] _beams = [new(4, 1, 2), new(5, 1, 3)];

    [Fact]
    public void Constructor_DefaultsTo200NewtonMetres_WithoutNameOrSides()
    {
        var brake = new BrakeDef(6, 1);

        brake.Strength.ShouldBe(BrakeDef.DefaultStrength);
        brake.Strength.ShouldBe(200 * 10000);
        brake.Locked.ShouldBeFalse();
        (brake.Name, brake.FixedLinkId, brake.TargetLinkId).ShouldBe((null, null, null));
    }

    [Theory]
    [InlineData(0, 1, null, null)]
    [InlineData(6, 0, null, null)]
    [InlineData(6, 1, 0, null)]
    [InlineData(6, 1, null, -4)]
    public void Constructor_WithoutPositiveIds_Throws(int id, int nodeId, int? fixedLinkId, int? targetLinkId)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new BrakeDef(id, nodeId, fixedLinkId, targetLinkId));
    }

    [Fact]
    public void Constructor_WithTheSameSideTwice_Throws()
    {
        Should.Throw<ArgumentException>(() => new BrakeDef(6, 1, 4, 4));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WithAStrengthThatIsNotPositive_Throws(double strength)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new BrakeDef(6, 1, strength: strength));
    }

    // Locked (#1039): the sides cannot turn against each other at all.
    [Fact]
    public void AnInfiniteStrength_IsLocked()
    {
        var brake = new BrakeDef(6, 1, strength: BrakeDef.LockedStrength);

        brake.Locked.ShouldBeTrue();
        brake.WithStrength(4000000).Locked.ShouldBeFalse();
    }

    [Fact]
    public void WithName_AndWithStrength_KeepItsIdAndSides()
    {
        var brake = new BrakeDef(6, 1, 4, 5);

        brake.WithName("Hold").ShouldBe(new BrakeDef(6, 1, 4, 5, "Hold"));
        brake.WithStrength(300000).ShouldBe(new BrakeDef(6, 1, 4, 5, strength: 300000));
    }

    [Fact]
    public void WithLinks_KeepsItsIdAndSettings_SinceItHasNoPorts()
    {
        var brake = new BrakeDef(6, 1, 4, 5, "Hold", strength: BrakeDef.LockedStrength);

        brake.WithLinks(5, null).ShouldBe(new BrakeDef(6, 1, 5, null, "Hold", strength: BrakeDef.LockedStrength));
    }

    [Fact]
    public void CreatureDef_AcceptsMissingSidesAndItsJointsWheel_ButNotAnotherJointsLinksOrWheel()
    {
        WheelDef[] wheels = [new(7, 1), new(8, 2)];

        var creature = Creature([new BrakeDef(6, 1, null, 7)], wheels: wheels);

        creature.Brakes.ShouldBe([new BrakeDef(6, 1, null, 7)]);
        creature.BrakeIndexOf(6).ShouldBe(0);
        Should.Throw<ArgumentOutOfRangeException>(() => creature.BrakeIndexOf(7));
        Should.Throw<ArgumentException>(() => Creature([new BrakeDef(6, 1, 4, 8)], wheels: wheels));
        Should.Throw<ArgumentException>(() => Creature([new BrakeDef(6, 2, 4, 5)]));
        Should.Throw<ArgumentException>(() => Creature([new BrakeDef(6, 9, 4, 5)]));
        Should.Throw<ArgumentException>(() => Creature([new BrakeDef(6, 1, 4, 99)]));
    }

    // A joint holds one Servo, Velocity motor or Brake (#1044).
    [Fact]
    public void CreatureDef_SharesTheJointsMotorOrBrakeSlot_WithAServoAMotorAndAnotherBrake()
    {
        Should.Throw<ArgumentException>(() => Creature([new BrakeDef(6, 1, 4, 5)], servos: [new ServoDef(7, 1, 5, 4)])).Message.ShouldContain("motor or brake");
        Should.Throw<ArgumentException>(() => Creature([new BrakeDef(6, 1, 4, 5)], motors: [new VelocityMotorDef(7, 1, 5, 4)])).Message.ShouldContain("motor or brake");
        Should.Throw<ArgumentException>(() => Creature([new BrakeDef(6, 1, 4, 5), new BrakeDef(7, 1, 5, 4)])).Message.ShouldContain("motor or brake");

        var apart = Creature([new BrakeDef(6, 1, 4, 5)], servos: [new ServoDef(7, 2, 4)], wheels: [new WheelDef(8, 1)]);
        apart.Brakes.ShouldHaveSingleItem().NodeId.ShouldBe(1);
    }

    // It is no joint motor: it has no ports, housing or selection yet (#1040, #1041).
    [Fact]
    public void CreatureDef_CountsABrakeAsAPart_ButNotAsAJointMotor()
    {
        var without = Creature([]);
        var with = Creature([new BrakeDef(6, 1, 4, 5)]);

        with.PartCount.ShouldBe(without.PartCount + 1);
        with.JointMotors.ShouldBeEmpty();
        with.NodeRadius(1).ShouldBe(without.NodeRadius(1));
        with.CoveredRadius(1).ShouldBe(without.CoveredRadius(1));
        Should.Throw<ArgumentOutOfRangeException>(() => Creature([new BrakeDef(10, 1)]));
        Should.Throw<ArgumentException>(() => Creature([new BrakeDef(4, 1)]));
    }

    [Fact]
    public void JsonRoundTrip_PreservesBrakes_WithLockedAsInfinity()
    {
        var original = Creature([new BrakeDef(6, 1, 4, 7, "Hold", BrakeDef.LockedStrength), new BrakeDef(8, 2, 4, strength: 300000)], wheels: [new WheelDef(7, 1)]);

        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<CreatureDef>(json);

        json.ShouldContain("\"Strength\":\"Infinity\"");
        roundTripped.ShouldNotBeNull();
        roundTripped.Brakes.ShouldBe(original.Brakes);
        roundTripped.Brakes[0].Locked.ShouldBeTrue();
    }

    private static CreatureDef Creature(BrakeDef[] brakes, ServoDef[]? servos = null, VelocityMotorDef[]? motors = null, WheelDef[]? wheels = null) =>
        new(_nodes, _beams, [], servos ?? [], [], [], wheels ?? [], [], [], motors ?? [], brakes, nextPartId: 10);
}
