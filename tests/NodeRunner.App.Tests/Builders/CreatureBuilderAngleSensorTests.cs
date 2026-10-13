using NodeRunner.App.Builders;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Builders;

public sealed class CreatureBuilderAngleSensorTests
{
    [Fact]
    public void AnAngleSensor_TakesTheServosDefaultSides_AndBuildsIntoTheCreature()
    {
        var builder = Joint(out var joint, out _, out var first, out var second);

        var sensor = builder.AddAngleSensor(joint);

        builder.AngleSensors.Single().ShouldBe(new AngleSensorDef(sensor, joint, first, second));
        builder.Build().AngleSensors.ShouldBe(builder.AngleSensors);
        new CreatureBuilder(builder.Build()).AngleSensors.ShouldBe(builder.AngleSensors);
    }

    [Fact]
    public void AnAngleSensor_TakesTheJointsWheel_AsASide()
    {
        var builder = new CreatureBuilder();
        var joint = builder.AddNode(new Vector2D(0, 0));
        var beam = builder.AddBeam(joint, builder.AddNode(new Vector2D(120, 0)));
        var wheel = builder.AddWheel(joint);

        var sensor = builder.AddAngleSensor(joint);

        builder.AngleSensors.Single().ShouldBe(new AngleSensorDef(sensor, joint, beam, wheel));
    }

    // A joint holds one Servo, Motor, Brake or Angle sensor (#1044).
    [Fact]
    public void AnAngleSensor_SharesItsJointsHingePartSlot_WithAServoAMotorABrakeAndAnotherAngleSensor()
    {
        var builder = Joint(out var joint, out var far, out _, out _);
        builder.AddAngleSensor(joint);

        builder.CanAddServo(joint, out var servoReason).ShouldBeFalse();
        servoReason.ShouldBe(CreatureBuilder.OneHingePartPerJointReason);
        builder.CanAddVelocityMotor(joint, out var motorReason).ShouldBeFalse();
        motorReason.ShouldBe(CreatureBuilder.OneHingePartPerJointReason);
        builder.CanAddBrake(joint, out var brakeReason).ShouldBeFalse();
        brakeReason.ShouldBe(CreatureBuilder.OneHingePartPerJointReason);
        Should.Throw<ArgumentException>(() => builder.AddAngleSensor(joint)).Message.ShouldBe(CreatureBuilder.OneHingePartPerJointReason.Message);

        builder.AddBrake(far);
        Should.Throw<ArgumentException>(() => builder.AddAngleSensor(far));
        Should.Throw<ArgumentException>(() => builder.AddAngleSensor(999)).Message.ShouldBe(CreatureBuilder.AngleSensorsGoOnAJointReason.Message);
        builder.CanAddAngleSensor(999, out var offJoint).ShouldBeFalse();
        offJoint.ShouldBe(CreatureBuilder.AngleSensorsGoOnAJointReason);
        builder.CanAddAngleSensor(far, out var taken).ShouldBeFalse();
        taken.ShouldBe(CreatureBuilder.OneHingePartPerJointReason);
    }

    [Fact]
    public void AnAngleSensorsSides_MustBeAtItsJoint_AndDifferent()
    {
        var builder = Joint(out var joint, out var far, out var first, out _);
        var elsewhere = builder.AddBeam(far, builder.AddNode(new Vector2D(240, 0)));

        Should.Throw<ArgumentException>(() => builder.AddAngleSensor(joint, first, elsewhere));
        Should.Throw<ArgumentException>(() => builder.AddAngleSensor(joint, first, first));
        builder.AngleSensors.ShouldBeEmpty();
    }

    [Fact]
    public void DeletingASide_LeavesItEmpty_AndDeletingTheJoint_RemovesTheAngleSensor()
    {
        var builder = Joint(out var joint, out _, out var first, out var second);
        var sensor = builder.AddAngleSensor(joint);

        builder.RemoveBeam(first);

        builder.AngleSensors.Single().ShouldBe(new AngleSensorDef(sensor, joint, null, second));
        builder.RemoveNode(joint);
        builder.AngleSensors.ShouldBeEmpty();
    }

    [Fact]
    public void DeletingAWheelSide_LeavesItEmpty()
    {
        var builder = new CreatureBuilder();
        var joint = builder.AddNode(new Vector2D(0, 0));
        var beam = builder.AddBeam(joint, builder.AddNode(new Vector2D(120, 0)));
        var wheel = builder.AddWheel(joint);
        var sensor = builder.AddAngleSensor(joint);

        builder.RemoveWheel(wheel);

        builder.AngleSensors.Single().ShouldBe(new AngleSensorDef(sensor, joint, beam, null));
    }

    [Fact]
    public void ALinkReplacingAnAngleSensorsBeam_TakesItsRole_AndTheSensorKeepsItsId()
    {
        var builder = Joint(out var joint, out var far, out _, out var second);
        var sensor = builder.AddAngleSensor(joint);

        var spring = builder.AddSpring(joint, far);

        builder.AngleSensors.Single().ShouldBe(new AngleSensorDef(sensor, joint, spring, second));
    }

    [Fact]
    public void Rename_NamesAnAngleSensor_AndRemoveAngleSensor_FreesItsSlot()
    {
        var builder = Joint(out var joint, out _, out _, out _);
        var sensor = builder.AddAngleSensor(joint);

        builder.Rename(sensor, "Knee");
        builder.AngleSensors.Single().Name.ShouldBe("Knee");
        builder.RemoveAngleSensor(sensor);

        builder.AngleSensors.ShouldBeEmpty();
        builder.CanAddServo(joint, out _).ShouldBeTrue();
    }

    [Fact]
    public void SetAngleSensorLink_PickingTheOtherRolesLink_SwapsTheSides_AndKeepsTheId()
    {
        var builder = Joint(out var joint, out _, out var first, out var second);
        var sensor = builder.AddAngleSensor(joint);

        builder.SetAngleSensorLink(sensor, fixedRole: true, second);

        builder.AngleSensors.Single().ShouldBe(new AngleSensorDef(sensor, joint, second, first));
        Should.Throw<ArgumentException>(() => builder.SetAngleSensorLink(sensor, fixedRole: true, linkId: 999));
    }

    // Inputs only (#1204): a new one adds its three inputs and leaves the brain's outputs as they were.
    [Fact]
    public void AnAngleSensor_AddsThreeInputs_AndNoOutputs()
    {
        var builder = Joint(out var joint, out _, out _, out _);
        builder.AddServo(builder.AddNode(new Vector2D(300, 0)));
        var before = BrainPorts.Of(builder.Build());

        var sensor = builder.AddAngleSensor(joint);

        var after = BrainPorts.Of(builder.Build());
        after.Inputs.Except(before.Inputs).ShouldBe(BrainPorts.AngleSensorInputs(sensor));
        after.Outputs.ShouldBe(before.Outputs);
        before.Outputs.ShouldNotBeEmpty();
    }

    [Fact]
    public void AnAngleSensor_HasNoSettings()
    {
        var builder = Joint(out var joint, out _, out _, out _);

        var sensor = builder.AddAngleSensor(joint);

        builder.ParametersOf(sensor).ShouldBeEmpty();
        builder.PartIdsWithSettings.ShouldNotContain(sensor);
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
