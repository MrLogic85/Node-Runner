using Godot;
using NodeRunner.Domain;

namespace NodeRunner.Theme;

/// <summary>
/// A Stepper (#1067, <c>docs/WORLD_VISUALS.md</c> → Stepper) at rotation 0: a detent wheel with one
/// V notch per step, which turns with the Fixed side, and a pawl on the Target side that sits in a
/// notch while it holds. Angles are Godot rotations, so a notch counted counter-clockwise on screen
/// (<c>Mechanics.Stepper.NotchAngle</c>) lies at a falling rotation.
/// </summary>
public static class StepperGeometry
{
    /// <summary>The Stepper's edge: the joint motor's radius, which it stands in for.</summary>
    public const float Radius = (float)JointMotor.Radius;

    /// <summary>The centre line of the notched rim, so its stroke's outer edge is <see cref="Radius"/>.</summary>
    public const float RimRadius = Radius - 1;

    /// <summary>Where the pawl's base lies, past the rim.</summary>
    public const float PawlBase = Radius + 6;

    public const float PawlHalfWidth = 6;

    /// <summary>Half the pawl's <c>stroke-signal</c> outline: how far its drawn edge lies past its shape.</summary>
    public const float PawlEdge = 1;

    /// <summary>The dark band round the pawl's outline that parts it from the rim.</summary>
    public const float PawlGap = 1;

    /// <summary>How far either side of the pawl's axis the selection mark leaves the rim for its keyhole.</summary>
    public const float KeyholeHalfWidth = PawlHalfWidth + PawlEdge + (float)SelectionMarks.Gap;

    /// <summary>The keyhole's far end, round the pawl's base.</summary>
    public const float KeyholeReach = PawlBase + PawlEdge + (float)SelectionMarks.Gap;

    /// <summary>
    /// The selection ring on a Touch sensor's joint: a joint's halo outside the cushion, pushed out
    /// to clear the pawl's drawn base corners by <see cref="SelectionMarks.Gap"/>.
    /// </summary>
    public static readonly float CushionedHaloRadius = MathF.Max(
        (float)SelectionMarks.JointHalo(Radius + TouchSensorDef.Cushion),
        new Vector2(PawlBase, PawlHalfWidth).Length() + PawlEdge + (float)SelectionMarks.Gap);

    public const float HubRingRadius = 8;

    public const float HubDiscRadius = 3.5f;

    private const float _mostMouthHalfWidth = 4.5f;
    private const float _mouthPerSpacing = 0.32f;
    private const float _mostDepth = 6;
    private const float _depthPerSpacing = 0.55f;
    private const int _arcSegmentsPerTurn = 96;

    /// <summary>The rim's length between two notches.</summary>
    public static float Spacing(int stepsPerTurn) => RimRadius * Mathf.Tau / stepsPerTurn;

    /// <summary>Half a notch's mouth, along the rim.</summary>
    public static float MouthHalfWidth(int stepsPerTurn) => MathF.Min(_mostMouthHalfWidth, _mouthPerSpacing * Spacing(stepsPerTurn));

    /// <summary>How deep a notch cuts in from the rim.</summary>
    public static float Depth(int stepsPerTurn) => MathF.Min(_mostDepth, (_depthPerSpacing * Spacing(stepsPerTurn)) + 1);

    /// <summary>
    /// Notch <paramref name="notch"/>'s rotation in the Fixed side's frame, counted counter-clockwise
    /// on screen from notch 0 at <paramref name="builtAngle"/>.
    /// </summary>
    public static float NotchAngle(int notch, int stepsPerTurn, float builtAngle) => builtAngle - (notch * Mathf.Tau / stepsPerTurn);

    /// <summary>
    /// Build's still angles: the rim along the Fixed side and the pawl along the Target, so notch 0
    /// sits on the Target as built: <c>Built</c>, the Target's direction in the rim's frame. With a side
    /// missing, both lie along the one there is.
    /// </summary>
    public static (float Rim, float Pawl, float Built) BuildAngles(float? fixedAngle, float? targetAngle)
    {
        var rim = fixedAngle ?? targetAngle ?? 0;
        var pawl = targetAngle ?? fixedAngle ?? 0;
        return (rim, pawl, pawl - rim);
    }

    /// <summary>
    /// Whether the notches are too small to see at <paramref name="pixelScale"/> window pixels a
    /// unit, so the rim draws as a plain circle: a mouth under <see cref="TriangleHatch.MinPixelSpacing"/>.
    /// </summary>
    public static bool IsTooSmall(int stepsPerTurn, float pixelScale) =>
        2 * MouthHalfWidth(stepsPerTurn) * pixelScale < TriangleHatch.MinPixelSpacing;

    /// <summary>
    /// Whether the rim is a plain circle: its notches too small to see, or a Training shadow, whose
    /// notches would tangle in a crowd while its pawl still shows each step.
    /// </summary>
    public static bool IsPlain(int stepsPerTurn, float pixelScale, bool simplified) =>
        simplified || IsTooSmall(stepsPerTurn, pixelScale);

    /// <summary>
    /// The notched outline at <paramref name="radius"/> as a closed loop, its notches as deep as the
    /// rim's and as wide along it, notch 0 at <paramref name="builtAngle"/>.
    /// </summary>
    public static Vector2[] Outline(int stepsPerTurn, float builtAngle, float radius = RimRadius)
    {
        var points = new List<Vector2>();
        var step = Mathf.Tau / stepsPerTurn;
        var half = MouthHalfWidth(stepsPerTurn) / radius;
        var depth = Depth(stepsPerTurn);
        var segments = Math.Max(2, (int)MathF.Ceiling(_arcSegmentsPerTurn / (float)stepsPerTurn));
        for (var notch = 0; notch < stepsPerTurn; notch++)
        {
            var at = NotchAngle(notch, stepsPerTurn, builtAngle);
            points.Add(Vector2.FromAngle(at + half) * radius);
            points.Add(Vector2.FromAngle(at) * (radius - depth));
            points.Add(Vector2.FromAngle(at - half) * radius);
            for (var i = 1; i < segments; i++)
            {
                points.Add(Vector2.FromAngle(at - half - ((step - (2 * half)) * i / segments)) * radius);
            }
        }

        points.Add(points[0]);
        return [.. points];
    }


    /// <summary>The pawl along rotation 0: seated, its tip at a notch's bottom; lifted, at the edge.</summary>
    public static Vector2[] Pawl(int stepsPerTurn, bool seated)
    {
        var tip = seated ? RimRadius - Depth(stepsPerTurn) : Radius;
        return [new(tip, 0), new(PawlBase, PawlHalfWidth), new(PawlBase, -PawlHalfWidth), new(tip, 0)];
    }

    /// <summary>
    /// The soft wedge from the pawl, along rotation 0, round to the notch it steps to,
    /// <paramref name="sweep"/> away, out to the rim.
    /// </summary>
    public static Vector2[] Wedge(float sweep)
    {
        var segments = Math.Max(2, (int)MathF.Ceiling(MathF.Abs(sweep) / Mathf.Tau * _arcSegmentsPerTurn));
        var points = new Vector2[segments + 2];
        points[0] = Vector2.Zero;
        for (var i = 0; i <= segments; i++)
        {
            points[i + 1] = Vector2.FromAngle(sweep * i / segments) * RimRadius;
        }

        return points;
    }

    /// <summary>
    /// The selection mark along rotation 0, as a closed loop: a ring <see cref="SelectionMarks.Gap"/> outside
    /// the Stepper's edge, which is the notched rim's own offset, as that fills every notch, with a keyhole
    /// round the pawl, or none without one.
    /// </summary>
    public static Vector2[] Halo(bool keyhole)
    {
        var radius = Radius + (float)SelectionMarks.Gap;
        var leave = keyhole ? MathF.Asin(KeyholeHalfWidth / radius) : 0;
        var segments = _arcSegmentsPerTurn;
        var points = new List<Vector2>(segments + 4);
        for (var i = 0; i <= segments; i++)
        {
            points.Add(Vector2.FromAngle(leave + ((Mathf.Tau - (2 * leave)) * i / segments)) * radius);
        }

        if (keyhole)
        {
            points.Add(new Vector2(KeyholeReach, -KeyholeHalfWidth));
            points.Add(new Vector2(KeyholeReach, KeyholeHalfWidth));
            points.Add(points[0]);
        }

        return [.. points];
    }
}
