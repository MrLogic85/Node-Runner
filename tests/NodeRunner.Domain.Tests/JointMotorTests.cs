namespace NodeRunner.Domain.Tests;

public sealed class JointMotorTests
{
    [Fact]
    public void HasTwoSides_CountsTheWheelAsALink()
    {
        LinkRef[] one = [new(3, 1, 2, CreatureElementKind.Beam)];

        JointMotor.HasTwoSides(one, hasWheel: true).ShouldBeTrue();
        JointMotor.HasTwoSides(one, hasWheel: false).ShouldBeFalse();
        JointMotor.HasTwoSides([], hasWheel: true).ShouldBeFalse();
    }

    [Fact]
    public void DefaultSides_TakesTwoLinks_ElseMakesTheWheelTheTarget()
    {
        LinkRef[] links = [new(9, 1, 3, CreatureElementKind.Beam), new(4, 1, 2, CreatureElementKind.Spring), new(7, 1, 4, CreatureElementKind.Piston)];
        var wheel = new WheelDef(5, 1);

        JointMotor.DefaultSides(links, wheel).ShouldBe((4, 7));
        JointMotor.DefaultSides(links[..1], wheel).ShouldBe((9, 5));
        JointMotor.DefaultSides([], wheel).ShouldBe((null, 5));
        JointMotor.DefaultSides(links[..1], null).ShouldBe((9, null));
    }
}
