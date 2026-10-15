using NodeRunner.App.Builders;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Builders;

public sealed class CreatureBuilderVelocityMotorTests
{
    [Fact]
    public void AVelocityMotor_SharesItsJointsMotorSlot_WithAServo()
    {
        var builder = Joint(out var joint, out _, out _, out _);
        var other = builder.AddNode(new Vector2D(300, 0));

        builder.AddVelocityMotor(joint);
        builder.AddServo(other);

        builder.CanAddServo(joint, out var servoReason).ShouldBeFalse();
        servoReason.ShouldBe(CreatureBuilder.OneHingePartPerJointReason);
        builder.CanAddVelocityMotor(other, out var motorReason).ShouldBeFalse();
        motorReason.ShouldBe(CreatureBuilder.OneHingePartPerJointReason);
        builder.CanAddVelocityMotor(joint, out _).ShouldBeFalse();
    }

    [Fact]
    public void AVelocityMotor_TakesTheServosDefaultSides()
    {
        var builder = Joint(out var joint, out _, out var first, out var second);

        var motor = builder.AddVelocityMotor(joint);

        builder.VelocityMotors.Single().ShouldBe(new VelocityMotorDef(motor, joint, first, second));
    }

    // Every link change keeps the motor's id, and so its network ports and training (#1177).
    [Fact]
    public void SetVelocityMotorLink_KeepsItsId_ThroughEveryLinkChange()
    {
        var builder = Joint(out var joint, out _, out var first, out var second);
        var third = builder.AddBeam(joint, builder.AddNode(new Vector2D(-90, 0)));
        var motor = builder.AddVelocityMotor(joint);
        var wheel = builder.AddWheel(joint);
        builder.RemoveBeam(first);
        var nextPartId = builder.NextPartId;

        builder.SetVelocityMotorLink(motor, fixedRole: true, third);
        builder.VelocityMotors.Single().ShouldBe(new VelocityMotorDef(motor, joint, third, second));
        builder.SetVelocityMotorLink(motor, fixedRole: false, wheel);
        builder.VelocityMotors.Single().ShouldBe(new VelocityMotorDef(motor, joint, third, wheel));
        builder.SetVelocityMotorLink(motor, fixedRole: true, wheel);
        builder.VelocityMotors.Single().ShouldBe(new VelocityMotorDef(motor, joint, wheel, third));
        builder.SetVelocityMotorLink(motor, fixedRole: false, second);
        builder.VelocityMotors.Single().ShouldBe(new VelocityMotorDef(motor, joint, wheel, second));

        builder.NextPartId.ShouldBe(nextPartId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ALinkReplacingAVelocityMotorsBeam_TakesItsRole_AndKeepsTheMotorsId(bool piston)
    {
        var builder = Joint(out var joint, out var far, out _, out var second);
        var motor = builder.AddVelocityMotor(joint);

        var link = piston ? builder.AddPiston(joint, far) : builder.AddSpring(joint, far);

        builder.VelocityMotors.Single().ShouldBe(new VelocityMotorDef(motor, joint, link, second));
        builder.NextPartId.ShouldBe(link + 1);
    }

    [Fact]
    public void DeletingASide_LeavesItEmpty_AndDeletingTheJoint_RemovesTheMotor()
    {
        var builder = Joint(out var joint, out _, out var first, out var second);
        var motor = builder.AddVelocityMotor(joint);

        builder.RemoveBeam(first);

        builder.VelocityMotors.Single().ShouldBe(new VelocityMotorDef(motor, joint, null, second));
        builder.RemoveNode(joint);
        builder.VelocityMotors.ShouldBeEmpty();
    }

    [Fact]
    public void ParametersOf_AVelocityMotor_AreItsOwnStrengthAndSpeed_AndTheServosRiseTime()
    {
        var builder = Joint(out var joint, out _, out _, out _);
        var motor = builder.AddVelocityMotor(joint);

        builder.ParametersOf(motor).ShouldBe([PartParameterId.MotorStrength, PartParameterId.MotorMaxSpeed, PartParameterId.RiseTime]);
        builder.SetParameter(motor, PartParameterId.MotorMaxSpeed, 2);
        builder.SetParameter(motor, PartParameterId.MotorStrength, 300000);
        builder.Build().VelocityMotors.Single().MaxSpeed.ShouldBe(2);
        builder.Build().VelocityMotors.Single().Strength.ShouldBe(300000);
        Should.Throw<ArgumentOutOfRangeException>(() => builder.SetParameter(motor, PartParameterId.ServoStrength, 1));
        Should.Throw<ArgumentOutOfRangeException>(() => builder.SetParameter(motor, PartParameterId.Range, 1));
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
