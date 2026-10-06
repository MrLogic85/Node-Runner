using NodeRunner.Domain;

namespace NodeRunner.Mechanics;

/// <summary>
/// The travel and rest length of a <see cref="SpringDef"/> (#835). It has a hard stop at each end
/// of its travel. Inside 0…1 its <see cref="SpringDef.Preload"/> says where the drawn length sits in
/// that travel, and the Spring pushes nothing there. Outside 0…1 the travel stays where 0 or 1 puts
/// it, with the drawn length on a stop, and only its rest length moves on past that stop, so the
/// Spring starts pressed against it, harder the further out.
/// </summary>
public static class Spring
{
    /// <summary>Its shortest length: the drawn length sits <see cref="SpringDef.Preload"/>, kept within 0…1, of the way to its longest.</summary>
    public static double ShortestLength(SpringDef spring, double builtLength)
    {
        ArgumentNullException.ThrowIfNull(spring);
        return Travel.Shortest(builtLength, spring.Stroke, Math.Clamp(spring.Preload, 0, 1));
    }

    /// <summary>Its longest length: its shortest grown by <see cref="SpringDef.Stroke"/>.</summary>
    public static double LongestLength(SpringDef spring, double builtLength) =>
        ShortestLength(spring, builtLength) * (1 + spring.Stroke);

    /// <summary>
    /// Where it pushes nothing: <see cref="SpringDef.Preload"/> of the way along its travel. That is
    /// the drawn length inside 0…1, and beyond a stop outside it.
    /// </summary>
    public static double RestLength(SpringDef spring, double builtLength) =>
        ShortestLength(spring, builtLength) * (1 + (spring.Preload * spring.Stroke));

    /// <summary>
    /// The rest length to give Godot's spring this step (#835): its own rest length, but past a stop
    /// it pushes at most what brings its nodes to that stop within the step.
    /// </summary>
    /// <remarks>
    /// A real preloaded spring presses its stop with its whole preload and the stop holds it inside,
    /// so its nodes feel nothing until a load beats the preload. Godot's spring would instead push
    /// its whole preload through the nodes into the end-stop joints, which give way under it and
    /// throw the creature about (measured headless on a creature at preload 2, stiffness 2000).
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
