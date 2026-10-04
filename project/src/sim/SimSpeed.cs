using Godot;

namespace NodeRunner.Sim;

/// <summary>
/// The simulation's fixed tick rate and the global speed-up (#787). Godot steps physics by
/// <c>1 / PhysicsTicksPerSecond × TimeScale</c>, so TimeScale alone stretches every step. Speeding up
/// raises both together: the step stays <c>1 / TicksPerSecond</c> and only more of them run per
/// second, so a run's result never depends on its speed.
/// </summary>
public static class SimSpeed
{
    /// <summary>Physics ticks per simulated second, from the project setting, whatever the speed.</summary>
    public static int TicksPerSecond { get; } =
        ProjectSettings.GetSetting("physics/common/physics_ticks_per_second").AsInt32();

    /// <summary>Runs the simulation <paramref name="speed"/> times faster than real time.</summary>
    public static void Set(int speed)
    {
        Engine.TimeScale = speed;
        Engine.PhysicsTicksPerSecond = TicksPerSecond * speed;
    }
}
