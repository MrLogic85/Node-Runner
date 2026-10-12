namespace NodeRunner.Domain;

/// <summary>What every joint motor, a Servo or a Velocity motor (<see cref="IJointMotorDef"/>), shares.</summary>
public static class JointMotor
{
    /// <summary>A joint motor's visible/collision joint radius, about 11/6 of a plain joint (#452).</summary>
    public const double Radius = 27;

    /// <summary>
    /// Whether a joint with <paramref name="linksAtJoint"/>, and a Wheel when <paramref name="hasWheel"/>,
    /// can hold a working motor: it needs a Fixed and a Target side, two links or a link and the Wheel.
    /// </summary>
    public static bool HasTwoSides(IReadOnlyCollection<LinkRef> linksAtJoint, bool hasWheel)
    {
        ArgumentNullException.ThrowIfNull(linksAtJoint);
        return linksAtJoint.Count + (hasWheel ? 1 : 0) >= 2;
    }

    /// <summary>
    /// A new motor's Fixed and Target on a joint with <paramref name="linksAtJoint"/> and
    /// <paramref name="wheel"/>: the two lowest-id links, or with fewer the link as Fixed and the
    /// Wheel as Target (#1118). A role nothing fills stays empty.
    /// </summary>
    public static (int? Fixed, int? Target) DefaultSides(IEnumerable<LinkRef> linksAtJoint, WheelDef? wheel)
    {
        ArgumentNullException.ThrowIfNull(linksAtJoint);
        var links = linksAtJoint.OrderBy(link => link.Id).Select(link => link.Id).Take(2).ToArray();
        return links.Length switch
        {
            2 => (links[0], links[1]),
            1 => (links[0], wheel?.Id),
            _ => (null, wheel?.Id),
        };
    }
}
