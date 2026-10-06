using NodeRunner.Domain;

namespace NodeRunner.Mechanics;

/// <summary>
/// The travel and rest length of a <see cref="SpringDef"/> (#835). Like a Piston's they are measured
/// on the gap between its joints' edges, where the movement happens. It has a hard stop at each end
/// of a travel, and its <see cref="SpringDef.CoilLength"/> moves its rest length evenly from half its
/// drawn gap short of its shortest stop to half its drawn gap past its longest. While the rest length
/// is inside the travel it is the drawn length, and the stops sit round it as a Piston's of the same
/// Stroke round a Start position there, so the travel is longer the nearer the shortest stop (#974);
/// past a stop the stops stay as a Piston's at that end with the drawn length on that stop, and the
/// Spring starts pressed against it, harder the further out.
/// </summary>
/// <remarks>
/// Every length takes the Spring's length as built, centre to centre, and its two joints' radii
/// together; a joint with a Servo is bigger, so its gap and travel are shorter.
/// </remarks>
public static class Spring
{
    /// <summary>Its shortest length: a Piston's of the same Stroke drawn where its rest length is, kept within its travel.</summary>
    public static double ShortestLength(SpringDef spring, double builtLength, double jointRadii)
    {
        ArgumentNullException.ThrowIfNull(spring);
        return Travel.Shortest(builtLength, jointRadii, spring.Stroke, At(spring, builtLength, jointRadii));
    }

    /// <summary>Its longest length: its shortest plus its <see cref="TravelLength"/>.</summary>
    public static double LongestLength(SpringDef spring, double builtLength, double jointRadii) =>
        ShortestLength(spring, builtLength, jointRadii) + TravelLength(spring, builtLength, jointRadii);

    /// <summary>Where it pushes nothing: <see cref="Offset"/> past its shortest stop.</summary>
    public static double RestLength(SpringDef spring, double builtLength, double jointRadii) =>
        ShortestLength(spring, builtLength, jointRadii) + Offset(spring, builtLength, jointRadii);

    /// <summary>
    /// How far its longest length is past its shortest: a Piston's travel at the same Stroke drawn where
    /// its rest length is, kept within its travel, so the nearer its shortest stop, the longer (#974).
    /// </summary>
    public static double TravelLength(SpringDef spring, double builtLength, double jointRadii)
    {
        ArgumentNullException.ThrowIfNull(spring);
        var at = At(spring, builtLength, jointRadii);
        return Travel.Longest(builtLength, jointRadii, spring.Stroke, at) - Travel.Shortest(builtLength, jointRadii, spring.Stroke, at);
    }

    // Where its drawn length sits in its travel, as a Piston's Start position (#974): where its
    // rest length is while that is inside the travel, else the stop it is pressed against.
    // Inside, a Piston drawn at `at` has its drawn gap g·at·s/(1 + at·s) past its shortest, so
    // `at` follows from the Offset.
    private static double At(SpringDef spring, double builtLength, double jointRadii)
    {
        var offset = Offset(spring, builtLength, jointRadii);
        if (offset <= 0)
        {
            return 0;
        }

        if (offset >= TravelDrawnAtLongest(spring, builtLength, jointRadii))
        {
            return 1;
        }

        return offset / (spring.Stroke * (Travel.Gap(builtLength, jointRadii) - offset));
    }

    // How far its rest length is past its shortest stop: from half the drawn gap short of it at
    // Coil length 0 to half the drawn gap past its longest at 1, evenly. Inside the travel its rest
    // length is its drawn length, so this is also where that sits.
    private static double Offset(SpringDef spring, double builtLength, double jointRadii)
    {
        var margin = Travel.Gap(builtLength, jointRadii) / 2;
        return (spring.CoilLength * ((2 * margin) + TravelDrawnAtLongest(spring, builtLength, jointRadii))) - margin;
    }

    // Its travel drawn on its longest stop, the shortest it gets, and how far Offset runs inside the travel.
    private static double TravelDrawnAtLongest(SpringDef spring, double builtLength, double jointRadii) =>
        Travel.Longest(builtLength, jointRadii, spring.Stroke, 1) - Travel.Shortest(builtLength, jointRadii, spring.Stroke, 1);

    /// <summary>
    /// The rest length to give Godot's spring this step (#835): its own rest length, but past a stop
    /// it pushes at most what brings its nodes to that stop within the step.
    /// </summary>
    /// <remarks>
    /// A real preloaded spring presses its stop with its whole preload and the stop holds it inside,
    /// so its nodes feel nothing until a load beats the preload. Godot's spring would instead push
    /// its whole preload through the nodes into the end-stop joints, which give way under it and
    /// throw the creature about (measured headless on a creature resting at 1.5 times its drawn length, stiffness 2000).
    /// Pushing only what reaches the stop keeps the preload inside it. Under a load it pushes back
    /// up to the full preload; <paramref name="reducedMass"/> counts only its two nodes, so against a
    /// heavier creature it gives a little rather than overshoot.
    /// </remarks>
    /// <param name="restLength">Its <see cref="RestLength"/>.</param>
    /// <param name="shortest">Its <see cref="ShortestLength"/>.</param>
    /// <param name="longest">Its <see cref="LongestLength"/>.</param>
    /// <param name="stiffness">Its Stiffness, in world force units per world unit.</param>
    /// <param name="length">Its length now.</param>
    /// <param name="speed">How fast its length grows, in world units per second.</param>
    /// <param name="reducedMass">Its two nodes' masses' product over their sum.</param>
    /// <param name="step">The physics step, in seconds.</param>
    public static double StepRestLength(
        double restLength,
        double shortest,
        double longest,
        double stiffness,
        double length,
        double speed,
        double reducedMass,
        double step)
    {
        var stop = restLength > longest ? longest : restLength < shortest ? shortest : restLength;
        if (stop == restLength)
        {
            return restLength;
        }

        var reach = reducedMass * (stop - length - (speed * step)) / (step * step);
        var reachedRest = length + (reach / stiffness);
        return restLength > longest
            ? Math.Min(restLength, Math.Max(length, reachedRest))
            : Math.Max(restLength, Math.Min(length, reachedRest));
    }
}
