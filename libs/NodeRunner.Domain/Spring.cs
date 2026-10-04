namespace NodeRunner.Domain;

/// <summary>
/// The physics numbers of a <see cref="SpringDef"/> (#453), shared by the sim and tests. The sim
/// builds it as Godot's <c>DampedSpringJoint2D</c>, which pulls with
/// <c>stiffness × (rest length − length)</c> and damps the speed between its ends with a damping
/// coefficient (force per speed). Stateless.
/// </summary>
public static class Spring
{
    /// <summary>
    /// The damping coefficient, in world force units per world unit per second, that gives
    /// <paramref name="spring"/> its <see cref="SpringDef.Damping"/> share of critical damping on
    /// its two nodes alone: <c>2 × damping × √(stiffness × pair mass)</c>, where the pair mass is
    /// mA·mB / (mA + mB). Attached to a heavier body it bounces a little more.
    /// </summary>
    /// <param name="spring">The Spring and its settings.</param>
    /// <param name="massA">The mass of its node A body.</param>
    /// <param name="massB">The mass of its node B body.</param>
    public static double DampingCoefficient(SpringDef spring, double massA, double massB)
    {
        ArgumentNullException.ThrowIfNull(spring);
        if (!double.IsFinite(massA) || massA <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(massA), "Node mass must be finite and positive.");
        }

        if (!double.IsFinite(massB) || massB <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(massB), "Node mass must be finite and positive.");
        }

        var pairMass = massA * massB / (massA + massB);
        return 2 * spring.Damping * Math.Sqrt(spring.Stiffness * pairMass);
    }
}
