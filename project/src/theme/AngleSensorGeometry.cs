using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// An Angle sensor's picture (#1205): a ring that stands in for its joint's, at a plain joint's size,
/// and an arc round it between the Fixed side's direction and the Target side's that fills and empties
/// with each turn (#1243, <see cref="Sweep"/>, <c>docs/WORLD_VISUALS.md</c> → Angle sensor).
/// Angles are Godot rotations, clockwise on screen, as the sides' are (<c>HingePartSides.FixedRotation</c>);
/// the network's angle (<see cref="Mechanics.AngleSensor"/>) is counter-clockwise on screen, so it is
/// the Fixed rotation minus the Target rotation.
/// </summary>
public static class AngleSensorGeometry
{
    /// <summary>The ring's outer edge: a plain joint's, which it stands in for.</summary>
    public const float Radius = (float)NodeDef.PlainJointRadius;

    /// <summary>The arc's width, <c>stroke-signal</c>, which is also the dot it draws at 0° (<see cref="AngleSensorDef.ArcWidth"/>).</summary>
    public const float ArcWidth = (float)AngleSensorDef.ArcWidth;

    /// <summary>The arc's <c>background</c> knock-out edge on each side, <c>stroke-hair</c>.</summary>
    public const float KnockoutWidth = UiSize.Stroke.Hair;
    private const int _segmentsPerTurn = 64;

    // Below this sweep the arc's ends are closer than a hundredth of a unit, so it draws as the dot.
    private const float _inLine = 0.0005f;

    // The sides' rotations are rounded separately, so sides in line can read a few ulps under a full
    // turn; within this they rest on turn -1, whose arc is the dot, as on turn 0.
    private const float _fullTurnSlack = 0.00001f;

    /// <summary>
    /// The angle the network reads, the Target side's direction minus the Fixed side's, counter-clockwise
    /// on screen, from 0 to just under a full turn.
    /// </summary>
    public static float Angle(float fixedRotation, float targetRotation) =>
        Mathf.PosMod(fixedRotation - targetRotation, Mathf.Tau);

    /// <summary>
    /// The turn the arc is on at rest, as Build, thumbnails and each trial's first frame draw it: 0, so
    /// the arc runs counter-clockwise from the Fixed side, or -1 for sides in line read just under a full turn.
    /// </summary>
    public static int RestTurn(float fixedRotation, float targetRotation) =>
        Angle(fixedRotation, targetRotation) > Mathf.Tau - _fullTurnSlack ? -1 : 0;

    /// <summary>
    /// How far the arc turns from the Fixed side to the Target side on <paramref name="turn"/>, as a Godot
    /// rotation: on an even turn the angle, counter-clockwise on screen (negative), so it fills; on an odd
    /// turn the rest of the ring, clockwise (positive), so it empties. As the Target side passes the Fixed
    /// side the turn changes where both turns draw a dot or both a full ring, so it never jumps.
    /// </summary>
    public static float Sweep(float fixedRotation, float targetRotation, int turn)
    {
        var angle = Angle(fixedRotation, targetRotation);
        return int.IsEvenInteger(turn) ? -angle : Mathf.Tau - angle;
    }

    /// <summary>
    /// The arc's centre line at <paramref name="radius"/>, from the Fixed side's direction by
    /// <see cref="Sweep"/> on <paramref name="turn"/>, at rest <see cref="RestTurn"/>: each end on its side's
    /// centre line. With the sides in line a single point, which draws as a dot <see cref="ArcWidth"/> across,
    /// on an even turn, as always at rest, and a full ring on an odd one; none with a side missing (null).
    /// </summary>
    public static Vector2[] ArcPoints(float radius, float? fixedRotation, float? targetRotation, int? turn = null)
    {
        if (fixedRotation is not { } from || targetRotation is not { } to)
        {
            return [];
        }

        var sweep = Sweep(from, to, turn ?? RestTurn(from, to));
        if (Mathf.Abs(sweep) < _inLine)
        {
            return [Vector2.FromAngle(from) * radius];
        }

        // Rounding error must not add a segment, so mirrored arcs draw alike.
        var segments = Math.Max(1, Mathf.CeilToInt((Mathf.Abs(sweep) / Mathf.Tau * _segmentsPerTurn) - 0.001f));
        return [.. Enumerable.Range(0, segments + 1).Select(step => Vector2.FromAngle(from + (sweep * step / segments)) * radius)];
    }
}
