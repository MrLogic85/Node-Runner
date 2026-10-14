using NodeRunner.App.Builders;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Builders;

public sealed class CreatureBuilderStepperTests
{
    [Fact]
    public void AStepper_TakesTheServosDefaultSides_AndBuildsIntoTheCreature()
    {
        var builder = Joint(out var joint, out _, out var first, out var second);

        var stepper = builder.AddStepper(joint);

        builder.Steppers.Single().ShouldBe(new StepperDef(stepper, joint, first, second));
        builder.Build().Steppers.ShouldBe(builder.Steppers);
        new CreatureBuilder(builder.Build()).Steppers.ShouldBe(builder.Steppers);
        builder.JointMotors.ShouldBe(builder.Steppers);
    }

    [Fact]
    public void AStepper_TakesTheJointsWheel_AsASide()
    {
        var builder = new CreatureBuilder();
        var joint = builder.AddNode(new Vector2D(0, 0));
        var beam = builder.AddBeam(joint, builder.AddNode(new Vector2D(120, 0)));
        var wheel = builder.AddWheel(joint);

        var stepper = builder.AddStepper(joint);

        builder.Steppers.Single().ShouldBe(new StepperDef(stepper, joint, beam, wheel));
    }

    // A joint holds one Servo, Motor, Brake, Angle sensor or Stepper (#1044, #1036).
    [Fact]
    public void AStepper_SharesItsJointsHingePartSlot_WithEveryOtherHingePart()
    {
        var builder = Joint(out var joint, out var far, out _, out _);
        builder.AddStepper(joint);

        builder.CanAddServo(joint, out var servoReason).ShouldBeFalse();
        servoReason.ShouldBe(CreatureBuilder.OneHingePartPerJointReason);
        builder.CanAddVelocityMotor(joint, out var motorReason).ShouldBeFalse();
        motorReason.ShouldBe(CreatureBuilder.OneHingePartPerJointReason);
        builder.CanAddBrake(joint, out var brakeReason).ShouldBeFalse();
        brakeReason.ShouldBe(CreatureBuilder.OneHingePartPerJointReason);
        builder.CanAddAngleSensor(joint, out var sensorReason).ShouldBeFalse();
        sensorReason.ShouldBe(CreatureBuilder.OneHingePartPerJointReason);
        Should.Throw<ArgumentException>(() => builder.AddStepper(joint)).Message.ShouldBe(CreatureBuilder.OneHingePartPerJointReason.Message);

        builder.AddAngleSensor(far);
        builder.CanAddStepper(far, out var taken).ShouldBeFalse();
        taken.ShouldBe(CreatureBuilder.OneHingePartPerJointReason);
        Should.Throw<ArgumentException>(() => builder.AddStepper(999)).Message.ShouldBe(CreatureBuilder.SteppersGoOnAJointReason.Message);
        builder.CanAddStepper(999, out var offJoint).ShouldBeFalse();
        offJoint.ShouldBe(CreatureBuilder.SteppersGoOnAJointReason);
    }

    [Fact]
    public void AStepperSides_MustBeAtItsJoint_AndDifferent()
    {
        var builder = Joint(out var joint, out var far, out var first, out _);
        var elsewhere = builder.AddBeam(far, builder.AddNode(new Vector2D(240, 0)));

        Should.Throw<ArgumentException>(() => builder.AddStepper(joint, first, elsewhere));
        Should.Throw<ArgumentException>(() => builder.AddStepper(joint, first, first));
        builder.Steppers.ShouldBeEmpty();
    }

    [Fact]
    public void DeletingASide_LeavesItEmpty_AndDeletingTheJoint_RemovesTheStepper()
    {
        var builder = Joint(out var joint, out _, out var first, out var second);
        var stepper = builder.AddStepper(joint);

        builder.RemoveBeam(first);

        builder.Steppers.Single().ShouldBe(new StepperDef(stepper, joint, null, second));
        builder.RemoveNode(joint);
        builder.Steppers.ShouldBeEmpty();
    }

    [Fact]
    public void ALinkReplacingAStepperBeam_TakesItsRole_AndTheStepperKeepsItsId()
    {
        var builder = Joint(out var joint, out var far, out _, out var second);
        var stepper = builder.AddStepper(joint);

        var spring = builder.AddSpring(joint, far);

        builder.Steppers.Single().ShouldBe(new StepperDef(stepper, joint, spring, second));
    }

    [Fact]
    public void Rename_NamesAStepper_AndRemoveStepper_FreesItsSlot()
    {
        var builder = Joint(out var joint, out _, out _, out _);
        var stepper = builder.AddStepper(joint);

        builder.Rename(stepper, "Ratchet");
        builder.Steppers.Single().Name.ShouldBe("Ratchet");
        builder.RemoveStepper(stepper);

        builder.Steppers.ShouldBeEmpty();
        builder.CanAddServo(joint, out _).ShouldBeTrue();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.RemoveStepper(stepper));
    }

    [Fact]
    public void SetStepperLink_PickingTheOtherRolesLink_SwapsTheSides_AndKeepsTheId()
    {
        var builder = Joint(out var joint, out _, out var first, out var second);
        var stepper = builder.AddStepper(joint);

        builder.SetStepperLink(stepper, fixedRole: true, second);

        builder.Steppers.Single().ShouldBe(new StepperDef(stepper, joint, second, first));
        Should.Throw<ArgumentException>(() => builder.SetStepperLink(stepper, fixedRole: true, linkId: 999));
    }

    // Steps per turn is a whole number of notches, so a slider's value rounds to one.
    [Fact]
    public void AStepper_HasItsThreeSettings_AndStepsPerTurn_RoundsToAWholeNotch()
    {
        var builder = Joint(out var joint, out _, out _, out _);
        var stepper = builder.AddStepper(joint);

        builder.ParametersOf(stepper).ShouldBe([PartParameterId.ServoStrength, PartParameterId.StepsPerTurn, PartParameterId.MaxFrequency]);
        builder.PartIdsWithSettings.ShouldContain(stepper);
        builder.SetParameter(stepper, PartParameterId.ServoStrength, 300000);
        builder.SetParameter(stepper, PartParameterId.StepsPerTurn, 11.6);
        builder.SetParameter(stepper, PartParameterId.MaxFrequency, 1.5);

        builder.Steppers.Single().ShouldBe(new StepperDef(stepper, joint, builder.Steppers[0].FixedLinkId, builder.Steppers[0].TargetLinkId, null, 300000, 12, 1.5));
        builder.ParameterValue(stepper, PartParameterId.StepsPerTurn).ShouldBe(12);
        builder.IsWithinBuildLimits().ShouldBeTrue();
    }

    // Build's sliders: Max strength 5–200 N·m (50000–2000000 world units), Max frequency 0.2–5 Hz.
    [Theory]
    [InlineData(PartParameterId.ServoStrength, 50000.0, true)]
    [InlineData(PartParameterId.ServoStrength, 2000000.0, true)]
    [InlineData(PartParameterId.ServoStrength, 49999.0, false)]
    [InlineData(PartParameterId.ServoStrength, 2000001.0, false)]
    [InlineData(PartParameterId.StepsPerTurn, 2.0, true)]
    [InlineData(PartParameterId.StepsPerTurn, 24.0, true)]
    [InlineData(PartParameterId.MaxFrequency, 0.2, true)]
    [InlineData(PartParameterId.MaxFrequency, 5.0, true)]
    [InlineData(PartParameterId.MaxFrequency, 0.19, false)]
    [InlineData(PartParameterId.MaxFrequency, 5.01, false)]
    public void IsWithinBuildLimits_TakesAStepperSettingOnItsSlider(PartParameterId parameter, double value, bool within)
    {
        var builder = Joint(out var joint, out _, out _, out _);

        builder.SetParameter(builder.AddStepper(joint), parameter, value);

        builder.IsWithinBuildLimits().ShouldBe(within);
    }

    // A Stepper's notches are its model, so StepperDef refuses a count off the slider.
    [Theory]
    [InlineData(1.0)]
    [InlineData(25.0)]
    public void StepsPerTurn_OffItsSlider_IsRefused(double stepsPerTurn)
    {
        var builder = Joint(out var joint, out _, out _, out _);
        var stepper = builder.AddStepper(joint);

        Should.Throw<ArgumentOutOfRangeException>(() => builder.SetParameter(stepper, PartParameterId.StepsPerTurn, stepsPerTurn));

        builder.Steppers.Single().StepsPerTurn.ShouldBe(StepperDef.DefaultStepsPerTurn);
    }

    // #1036: a frequency to step at and a power to push with, read from where the sides stand.
    [Fact]
    public void AStepper_AddsItsThreeInputs_AndItsTwoOutputs()
    {
        var builder = Joint(out var joint, out _, out _, out _);
        var before = BrainPorts.Of(builder.Build());

        var stepper = builder.AddStepper(joint);

        var after = BrainPorts.Of(builder.Build());
        after.Inputs.Except(before.Inputs).ShouldBe(BrainPorts.StepperInputs(stepper));
        after.Outputs.Except(before.Outputs).ShouldBe(BrainPorts.StepperOutputs(stepper));
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
