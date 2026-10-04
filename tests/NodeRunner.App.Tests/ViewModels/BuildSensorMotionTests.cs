using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Mechanics;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BuildSensorMotionTests
{
    private const double _gravity = 980;
    private const double _frame = 1.0 / 60;

    [Fact]
    public void Advance_FirstSeen_HangsAtRestAndIsStill()
    {
        var motion = new BuildSensorMotion();

        motion.Advance([Pose(new Vector2D(0, 0))], _frame, _gravity).ShouldBeFalse();

        motion.WeightOffset(1).ShouldBe(Accelerometer.RestWeightOffset(0, -1));
    }

    [Fact]
    public void Advance_ABeamAtRest_StaysStill()
    {
        var motion = new BuildSensorMotion();
        motion.Advance([Pose(new Vector2D(0, 0))], _frame, _gravity);

        for (var i = 0; i < 30; i++)
        {
            motion.Advance([Pose(new Vector2D(0, 0))], _frame, _gravity).ShouldBeFalse();
        }

        motion.WeightOffset(1).ShouldBe(Accelerometer.RestWeightOffset(0, -1));
    }

    [Fact]
    public void Advance_ADraggedBeam_SwingsTheWeightThenSettlesBack()
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
        var frames = 0;
        while (motion.Advance([Pose(new Vector2D(x, 0))], _frame, _gravity))
        {
            frames++;
            frames.ShouldBeLessThan(600);
        }

        motion.WeightOffset(1).ShouldBe(Accelerometer.RestWeightOffset(0, -1));
    }

    [Fact]
    public void Advance_ATurnedBeam_SwingsToItsNewRest()
    {
        var motion = new BuildSensorMotion();
        motion.Advance([Pose(new Vector2D(0, 0))], _frame, _gravity);

        motion.Advance([Pose(new Vector2D(0, 0), rotation: -Math.PI / 4)], _frame, _gravity).ShouldBeTrue();
        while (motion.Advance([Pose(new Vector2D(0, 0), rotation: -Math.PI / 4)], _frame, _gravity))
        {
        }

        motion.WeightOffset(1).ShouldBe(Accelerometer.RestWeightOffset(-Math.PI / 4, -1));
    }

    [Fact]
    public void Advance_WhenTheBeamsUpSideFlips_StartsAgainAtRest()
    {
        var motion = new BuildSensorMotion();
        motion.Advance([Pose(new Vector2D(0, 0))], _frame, _gravity);

        motion.Advance([Pose(new Vector2D(0, 0), rotation: Math.PI, upSign: 1)], _frame, _gravity).ShouldBeFalse();

        motion.WeightOffset(1).ShouldBe(Accelerometer.RestWeightOffset(Math.PI, 1));
    }

    [Fact]
    public void Advance_ForgetsASensorNoLongerListed()
    {
        var motion = new BuildSensorMotion();
        motion.Advance([Pose(new Vector2D(0, 0))], _frame, _gravity);

        motion.Advance([], _frame, _gravity);

        motion.WeightOffset(1).ShouldBeNull();
    }

    private static SensorPose Pose(Vector2D midpoint, double rotation = 0, int upSign = -1) => new(1, midpoint, rotation, upSign);
}
