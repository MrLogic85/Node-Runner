using NodeRunner.Domain;

namespace NodeRunner.Mechanics;

/// <summary>
/// What a <see cref="WheelDef"/> weighs and how hard it is to set turning (#129). Its weight is in the
/// tyre, a thin ring at its radius, so it turns as a ring does; Godot would take a circle collider for a
/// solid disc, half that. What its joint carries of its links sits on the axle and adds nothing.
/// </summary>
public static class Wheel
{
    /// <summary>How much a Wheel weighs per world unit of radius: 0.03 kg, 3 kg per metre.</summary>
    public const double MassPerRadius = 0.03;

    /// <summary>What it weighs in kg, <see cref="MassPerRadius"/> times its radius: 1.2 kg at 0.4 m, 3 kg at 1 m.</summary>
    public static double Mass(WheelDef wheel)
    {
        ArgumentNullException.ThrowIfNull(wheel);
        return MassPerRadius * wheel.Radius;
    }

    /// <summary>Its turning inertia, mass × radius², in kg·world unit², the units Godot's bodies take.</summary>
    public static double Inertia(WheelDef wheel)
    {
        ArgumentNullException.ThrowIfNull(wheel);
        return Mass(wheel) * wheel.Radius * wheel.Radius;
    }
}
