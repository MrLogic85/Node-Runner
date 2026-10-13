using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// An Angle sensor's picture (#1205): a ring that stands in for its joint's, at a plain joint's size,
/// and an arc round it from the Fixed side's direction to the Target side's, over the shortest angle.
/// Angles are Godot rotations, clockwise on screen, as the sides' are (<c>MotorSides.FixedRotation</c>);
/// the brain's angle (<see cref="Mechanics.AngleSensor"/>) is counter-clockwise on screen, so it is
/// the Fixed rotation minus the Target rotation.
/// </summary>
public static class AngleSensorGeometry
{
    /// <summary>The ring's outer edge: a plain joint's, which it stands in for.</summary>
    public const float Radius = (float)NodeDef.PlainJointRadius;

    /// <summary>The arc's width, <c>stroke-signal</c>, which is also the dot it draws at 0°.</summary>
    public const float ArcWidth = UiSize.Stroke.Signal;

    /// <summary>The arc's <c>background</c> knock-out edge on each side, <c>stroke-hair</c>.</summary>
    public const float KnockoutWidth = UiSize.Stroke.Hair;

    /// <summary>The selection halo's width, <c>stroke-signal</c>.</summary>
    public const float HaloWidth = UiSize.Stroke.Signal;

    private const float _gap = (float)SelectionMarks.Gap;
    private const int _segmentsPerTurn = 64;

    // Below this sweep the arc's ends are closer than a hundredth of a unit, so it draws as the dot.
    private const float _inLine = 0.0005f;

    // The sides' rotations are rounded separately, so a straight limb can read a few ulps past half a
    // turn; within this it still counts as exactly half a turn and runs counter-clockwise.
    private const float _halfTurnSlack = 0.00001f;

    /// <summary>
    /// The arc's centre line: <see cref="SelectionMarks.Gap"/> outside the joint's outermost part, a
    /// Touch sensor's cushion over a Rubber's skin over the ring. A Wheel on the joint changes nothing,
    /// so on its joint the arc lies over the tyre's inside and tubes.
    /// </summary>
    public static float ArcRadius(bool rubber, bool touch) =>
        Radius
        + (touch ? (float)TouchSensorDef.Cushion : rubber ? (float)RubberDef.Skin : 0)
        + _gap
        + (ArcWidth / 2);

    /// <summary>The selection halo's centre line, <see cref="SelectionMarks.Gap"/> outside the arc's outer edge.</summary>
    public static float HaloRadius(float arcRadius) => arcRadius + (ArcWidth / 2) + _gap + (HaloWidth / 2);

    /// <summary>
    /// The angle the brain reads, the Target side's direction minus the Fixed side's, counter-clockwise
    /// on screen, from 0 to just under a full turn.
    /// </summary>
    public static float Angle(float fixedRotation, float targetRotation) =>
        Mathf.PosMod(fixedRotation - targetRotation, Mathf.Tau);

    /// <summary>
    /// How far the arc turns from the Fixed side to the Target side, as a Godot rotation: the shortest
    /// way, negative (counter-clockwise on screen) up to and at half a turn, positive past it. It flips
    /// at half a turn with no hysteresis, and is 0 with the sides in line.
    /// </summary>
    public static float Sweep(float fixedRotation, float targetRotation)
    {
        var angle = Angle(fixedRotation, targetRotation);
        return angle <= Mathf.Pi + _halfTurnSlack ? -angle : Mathf.Tau - angle;
    }

    /// <summary>
    /// The arc's centre line at <paramref name="radius"/>, from the Fixed side's direction by
    /// <see cref="Sweep"/>: each end on its side's centre line. A single point with the sides in line,
    /// which draws as a dot <see cref="ArcWidth"/> across, and none with a side missing (null).
    /// </summary>
    public static Vector2[] ArcPoints(float radius, float? fixedRotation, float? targetRotation)
    {
        if (fixedRotation is not { } from || targetRotation is not { } to)
        {
            return [];
        }

        var sweep = Sweep(from, to);
        if (Mathf.Abs(sweep) < _inLine)
        {
            return [Vector2.FromAngle(from) * radius];
        }

        // Rounding error must not add a segment, so mirrored arcs draw alike.
        var segments = Math.Max(1, Mathf.CeilToInt((Mathf.Abs(sweep) / Mathf.Tau * _segmentsPerTurn) - 0.001f));
        return [.. Enumerable.Range(0, segments + 1).Select(step => Vector2.FromAngle(from + (sweep * step / segments)) * radius)];
    }
}
