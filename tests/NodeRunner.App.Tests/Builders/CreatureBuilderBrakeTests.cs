using NodeRunner.App.Builders;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Builders;

public sealed class CreatureBuilderBrakeTests
{
    [Fact]
    public void ABrake_TakesTheServosDefaultSides_AndBuildsIntoTheCreature()
    {
        var builder = Joint(out var joint, out _, out var first, out var second);

        var brake = builder.AddBrake(joint);

        builder.Brakes.Single().ShouldBe(new BrakeDef(brake, joint, first, second));
        builder.Build().Brakes.ShouldBe(builder.Brakes);
        new CreatureBuilder(builder.Build()).Brakes.ShouldBe(builder.Brakes);
    }

    [Fact]
    public void ABrake_TakesTheJointsWheel_AsASide()
    {
        var builder = new CreatureBuilder();
        var joint = builder.AddNode(new Vector2D(0, 0));
        var beam = builder.AddBeam(joint, builder.AddNode(new Vector2D(120, 0)));
        var wheel = builder.AddWheel(joint);

        var brake = builder.AddBrake(joint);

        builder.Brakes.Single().ShouldBe(new BrakeDef(brake, joint, beam, wheel));
    }

    // A joint holds one Servo, Motor or Brake (#1044).
    [Fact]
    public void ABrake_SharesItsJointsMotorOrBrakeSlot_WithAServoAMotorAndAnotherBrake()
    {
        var builder = Joint(out var joint, out var far, out _, out _);
        builder.AddBrake(joint);

        builder.CanAddServo(joint, out var servoReason).ShouldBeFalse();
        servoReason.ShouldBe(CreatureBuilder.OneMotorOrBrakePerJointReason);
        builder.CanAddVelocityMotor(joint, out var motorReason).ShouldBeFalse();
        motorReason.ShouldBe(CreatureBuilder.OneMotorOrBrakePerJointReason);
        Should.Throw<ArgumentException>(() => builder.AddBrake(joint)).Message.ShouldBe(CreatureBuilder.OneMotorOrBrakePerJointReason.Message);

        builder.AddServo(far);
        Should.Throw<ArgumentException>(() => builder.AddBrake(far));
        Should.Throw<ArgumentOutOfRangeException>(() => builder.AddBrake(999));
    }

    [Fact]
    public void ABrakesSides_MustBeAtItsJoint()
    {
        var builder = Joint(out var joint, out var far, out var first, out _);
        var elsewhere = builder.AddBeam(far, builder.AddNode(new Vector2D(240, 0)));

        Should.Throw<ArgumentException>(() => builder.AddBrake(joint, first, elsewhere));
        builder.Brakes.ShouldBeEmpty();
    }

    [Fact]
    public void DeletingASide_LeavesItEmpty_AndDeletingTheJoint_RemovesTheBrake()
    {
        var builder = Joint(out var joint, out _, out var first, out var second);
        var brake = builder.AddBrake(joint);
        builder.SetBrakeStrength(brake, BrakeDef.LockedStrength);

        builder.RemoveBeam(first);

        builder.Brakes.Single().ShouldBe(new BrakeDef(brake, joint, null, second, strength: BrakeDef.LockedStrength));
        builder.RemoveNode(joint);
        builder.Brakes.ShouldBeEmpty();
    }

    [Fact]
    public void DeletingAWheelSide_LeavesItEmpty()
    {
        var builder = new CreatureBuilder();
        var joint = builder.AddNode(new Vector2D(0, 0));
        var beam = builder.AddBeam(joint, builder.AddNode(new Vector2D(120, 0)));
        var wheel = builder.AddWheel(joint);
        var brake = builder.AddBrake(joint);

        builder.RemoveWheel(wheel);

        builder.Brakes.Single().ShouldBe(new BrakeDef(brake, joint, beam, null));
    }

    [Fact]
    public void ALinkReplacingABrakesBeam_TakesItsRole_AndTheBrakeKeepsItsId()
    {
        var builder = Joint(out var joint, out var far, out _, out var second);
        var brake = builder.AddBrake(joint);

        var spring = builder.AddSpring(joint, far);

        builder.Brakes.Single().ShouldBe(new BrakeDef(brake, joint, spring, second));
    }

    [Fact]
    public void Rename_NamesABrake_AndRemoveBrake_FreesItsSlot()
    {
        var builder = Joint(out var joint, out _, out _, out _);
        var brake = builder.AddBrake(joint);

        builder.Rename(brake, "Hold");
        builder.Brakes.Single().Name.ShouldBe("Hold");
        builder.RemoveBrake(brake);

        builder.Brakes.ShouldBeEmpty();
        builder.CanAddServo(joint, out _).ShouldBeTrue();
    }

    // A Brake has no brain ports (#1039): adding one leaves the brain as it was.
    [Fact]
    public void ABrake_DeclaresNoPorts()
    {
        var builder = Joint(out var joint, out _, out _, out _);
        builder.AddServo(builder.AddNode(new Vector2D(300, 0)));
        var before = BrainPorts.Of(builder.Build());

        builder.AddBrake(joint);

        var after = BrainPorts.Of(builder.Build());
        after.Inputs.ShouldBe(before.Inputs);
        after.Outputs.ShouldBe(before.Outputs);
        before.Outputs.ShouldNotBeEmpty();
    }

    // Build's slider is 20–400 N·m (200000–4000000 world units) or Locked (#1041).
    [Theory]
    [InlineData(200000.0, true)]
    [InlineData(BrakeDef.DefaultStrength, true)]
    [InlineData(4000000.0, true)]
    [InlineData(BrakeDef.LockedStrength, true)]
    [InlineData(199999.0, false)]
    [InlineData(4000001.0, false)]
    public void IsWithinBuildLimits_TakesABrakesStrengthOnItsSlider_OrLocked(double strength, bool within)
    {
        var builder = Joint(out var joint, out _, out _, out _);

        builder.SetBrakeStrength(builder.AddBrake(joint), strength);

        builder.IsWithinBuildLimits().ShouldBe(within);
    }

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
}
