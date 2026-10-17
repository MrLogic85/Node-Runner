using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Mechanics;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BuildSensorMotionTests
{
    private const double _gravity = 980;
    private const double _frame = 1.0 / 60;

    [Fact]
    public void Advance_ABeamAtRest_HangsAtRestAndIsStill()
    {
        var motion = new BuildSensorMotion();

        for (var i = 0; i < 30; i++)
        {
            motion.Advance([Pose(new Vector2D(0, 0))], _frame, _gravity).ShouldBeFalse();
        }

        motion.WeightOffset(1).ShouldBe(Accelerometer.RestWeightOffset(0, -1));
    }

    [Fact]
    public void Advance_ADraggedOrTurnedBeam_SwingsTheWeightThenSettlesToItsRest()
    {
        var motion = new BuildSensorMotion();
        var x = 0.0;
        motion.Advance([Pose(new Vector2D(x, 0))], _frame, _gravity);

        var swung = false;
        for (var i = 0; i < 20; i++)
        {
            x += 15;
            motion.Advance([Pose(new Vector2D(x, 0))], _frame, _gravity).ShouldBeTrue();
            swung |= Math.Abs(motion.WeightOffset(1)!.Value.X) > 0.1;
        }

        swung.ShouldBeTrue();
        Settle(motion, Pose(new Vector2D(x, 0)));
        motion.WeightOffset(1).ShouldBe(Accelerometer.RestWeightOffset(0, -1));

        motion.Advance([Pose(new Vector2D(x, 0), rotation: -Math.PI / 4)], _frame, _gravity).ShouldBeTrue();
        Settle(motion, Pose(new Vector2D(x, 0), rotation: -Math.PI / 4));
        motion.WeightOffset(1).ShouldBe(Accelerometer.RestWeightOffset(-Math.PI / 4, -1));
    }

    [Fact]
    public void Advance_WhenTheUpSideFlipsOrTheSensorGoes_StartsAgainOrForgetsIt()
    {
        var motion = new BuildSensorMotion();
        motion.Advance([Pose(new Vector2D(0, 0))], _frame, _gravity);

        motion.Advance([Pose(new Vector2D(0, 0), rotation: Math.PI, upSign: 1)], _frame, _gravity).ShouldBeFalse();
        motion.WeightOffset(1).ShouldBe(Accelerometer.RestWeightOffset(Math.PI, 1));

        motion.Advance([], _frame, _gravity);
        motion.WeightOffset(1).ShouldBeNull();
    }

    private static void Settle(BuildSensorMotion motion, SensorPose pose)
    {
        var frames = 0;
        while (motion.Advance([pose], _frame, _gravity))
        {
            (++frames).ShouldBeLessThan(600);
        }
    }

    private static SensorPose Pose(Vector2D midpoint, double rotation = 0, int upSign = -1) => new(1, midpoint, rotation, upSign);
}
