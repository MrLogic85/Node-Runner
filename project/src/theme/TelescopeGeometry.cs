namespace NodeRunner.Theme;

/// <summary>
/// Where a Piston's or Spring's three-part telescope sits: an outer cylinder at joint A, a
/// middle stage and a rod. <c>docs/WORLD_VISUALS.md</c> → "Piston" owns the rule. All lengths are
/// world units from joint A's centre.
/// </summary>
public static class TelescopeGeometry
{
    /// <summary>A Piston's smallest parts: it still reads as three parts at Stroke 10%.</summary>
    public static readonly TelescopeMinimums Piston = new(Cylinder: 6, Stage: 5, Rod: 5);

    /// <summary>A Spring's smallest parts, longer than a Piston's so they show past its seats.</summary>
    public static readonly TelescopeMinimums Spring = new(Cylinder: 9, Stage: 5, Rod: 7);

    /// <summary>The telescope's parts at <paramref name="length"/>.</summary>
    /// <param name="minimums">The smallest parts: <see cref="Piston"/> or <see cref="Spring"/>.</param>
    /// <param name="length">The current length, centre to centre.</param>
    /// <param name="shortest">The shortest length, centre to centre, which sets the parts' sizes.</param>
    /// <param name="longest">The longest length, centre to centre.</param>
    /// <param name="coveredA">What covers joint A's end, where the cylinder starts.</param>
    /// <param name="coveredB">What covers joint B's end, where the cap or seat sits.</param>
    public static Telescope Place(TelescopeMinimums minimums, float length, float shortest, float longest, float coveredA, float coveredB)
    {
        var travel = Math.Max(longest - shortest, 0);
        var slide = travel / 2;
        var room = shortest - coveredB - coveredA;
        var need = minimums.Cylinder + minimums.Stage + minimums.Rod;
        float cylinderEnd;
        float lip;
        if (room >= need)
        {
            cylinderEnd = Math.Min(
                Math.Max(JointDrawing.HubRadius + slide, coveredA + minimums.Cylinder),
                shortest - coveredB - minimums.Rod - minimums.Stage);
            lip = minimums.Stage;
        }
        else
        {
            var share = Math.Max(room, 0) / need;
            cylinderEnd = coveredA + (share * minimums.Cylinder);
            lip = share * minimums.Stage;
        }

        var extension = travel > 0 ? Math.Clamp((length - shortest) / travel, 0, 1) : 0;
        var cap = length - coveredB;
        return new Telescope(coveredA, Math.Min(cylinderEnd, cap), Math.Min(cylinderEnd + lip + (extension * slide), cap), cap);
    }
}

/// <summary>A telescope's smallest cylinder, stage lip and exposed rod, in world units.</summary>
public readonly record struct TelescopeMinimums(float Cylinder, float Stage, float Rod);

/// <summary>
/// A telescope's parts, from joint A's centre: the cylinder from <see cref="Start"/> to
/// <see cref="CylinderEnd"/>, the stage from <see cref="Start"/>, under the cylinder, to its mouth
/// at <see cref="StageEnd"/>, and the cap or seat at <see cref="Cap"/>. Neither tube reaches past
/// <see cref="Cap"/>; one that would end at or before <see cref="Start"/> is empty and not drawn.
/// </summary>
public readonly record struct Telescope(float Start, float CylinderEnd, float StageEnd, float Cap)
{
    public bool HasCylinder => CylinderEnd > Start;

    public bool HasStage => StageEnd > Start;
}
