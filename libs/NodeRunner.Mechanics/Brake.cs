namespace NodeRunner.Mechanics;

/// <summary>
/// The torque of a <see cref="Domain.BrakeDef"/> (#1040): passive friction between its two sides.
/// Each step it asks for the torque that stops the turning between them and holds against what
/// the rest of the creature and the world push with, and gives at most its Strength of it. Within
/// Strength the joint grips and stays put; past it the joint slips, braked by the full Strength.
/// It has no preferred angle: it holds wherever the joint stopped. Godot welds a Locked Brake's
/// sides instead, unless no Godot joint can, and then it comes here with an infinite Strength
/// (project/src/creature/Creature.Links.cs, WeldSides).
/// </summary>
public static class Brake
{
    /// <summary>
    /// The Brake's next state from its last, or from none on its first step. <paramref name="speed"/>
    /// is the Target's turning speed against the Fixed side in radians per second, and
    /// <paramref name="strength"/> the most torque it gives, infinite for a Locked Brake that
    /// Godot cannot weld.
    /// </summary>
    /// <remarks>
    /// Both parts scale with <paramref name="effectiveInertia"/>: one takes away the turning, the
    /// other holds against the torque the other parts and the world put on the joint, read from how
    /// the last step changed its speed beyond what the brake did. That reading is a step late, so a
    /// sudden load turns the joint a little before it is held, and a sudden release kicks it back
    /// about as far, since the brake still holds against the load for that step. It is read while
    /// slipping too, so the brake grips as soon as the load is within its Strength. The loop stays
    /// steady while the inertia given is at most about a third heavier than the joint's, and chatters
    /// past that, so give a lower bound (Creature's HingePartSides.BrakeInertia); a lighter one only
    /// makes the joint give more, and swing back a little, before it holds. Each Brake reads its own
    /// joint alone, so Brakes on a shared link also read each other's torque as load; in Godot a
    /// straight chain of them only jitters by a fraction of a degree (#1040).
    /// </remarks>
    public static BrakeState Next(double speed, double strength, BrakeState? previous, double step, double effectiveInertia)
    {
        MotorLoop.RequirePositive(effectiveInertia, nameof(effectiveInertia), "Effective inertia");
        MotorLoop.RequirePositive(step, nameof(step), "Step");
        if (double.IsNaN(strength) || strength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(strength), "Brake strength must be positive, or infinite when locked.");
        }

        var torquePerSpeed = effectiveInertia / step;
        var load = previous is { } last ? (torquePerSpeed * (speed - last.Speed)) - last.Torque : 0;
        var torque = Math.Clamp((-torquePerSpeed * speed) - load, -strength, strength);
        return new BrakeState(torque, speed);
    }
}

/// <summary>
/// A Brake between steps (<see cref="Brake.Next"/>): the torque it gave and the speed it read, so
/// the next step can tell what else turned the joint.
/// </summary>
public readonly record struct BrakeState(double Torque, double Speed);
