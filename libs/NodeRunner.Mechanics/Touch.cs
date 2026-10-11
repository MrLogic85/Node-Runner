using NodeRunner.Domain;

namespace NodeRunner.Mechanics;

/// <summary>
/// A Touch sensor's pure math (#1045): how far the nearest ground reaches into its joint's cushion
/// (<see cref="TouchSensorDef.Cushion"/>). The sim finds that ground in any direction, so a slope
/// or a step reads like flat ground. See docs/CREATURE_MODEL.md → "Touch sensor".
/// </summary>
public static class Touch
{
    /// <summary>
    /// Its brain input: 0 with the ground at the cushion's outer edge or farther, rising linearly to
    /// 1 where the ground meets the joint. The ground is <paramref name="groundDistance"/> from the
    /// joint's centre and the joint's edge <paramref name="jointRadius"/>.
    /// </summary>
    public static double Reading(double groundDistance, double jointRadius) =>
        Math.Clamp((jointRadius + TouchSensorDef.Cushion - groundDistance) / TouchSensorDef.Cushion, 0, 1);
}
