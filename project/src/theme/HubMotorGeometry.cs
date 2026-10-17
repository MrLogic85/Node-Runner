using Godot;
using NodeRunner.Domain;

namespace NodeRunner.Theme;

/// <summary>
/// The Velocity motor's hub motor in section, at rotation 0: magnets that turn with the
/// Target side and coils that turn with the Fixed side. The coils end inside the magnets' inner
/// edge, so the two can turn apart on their own layers without either redrawing.
/// </summary>
public static class HubMotorGeometry
{
    /// <summary>The motor's edge: a joint motor's radius, which it covers.</summary>
    public const float Radius = (float)JointMotor.Radius;

    /// <summary>The centre line of the outer ring, whose outer edge is <see cref="Radius"/>.</summary>
    public const float RingRadius = Radius - 1;

    public const int MagnetCount = 6;
    public const float MagnetInnerRadius = 19.5f;
    public const float MagnetOuterRadius = 23;

    public const float MagnetHalfSpan = 20 * Mathf.Pi / 180;

    public const int CoilCount = 6;
    public const float CoilInnerRadius = 7;

    /// <summary>Where each coil's stem ends and its curved cap runs.</summary>
    public const float CoilOuterRadius = 15.5f;

    /// <summary>Half the angle each coil's cap spans.</summary>
    public const float CoilHalfSpan = 19 * Mathf.Pi / 180;

    public const float CentreRadius = 6;
    public const float AxleRadius = 2.2f;

    private const int _arcSegments = 8;

    /// <summary>The centre angle of magnet <paramref name="index"/>, a sixth of a turn apart.</summary>
    public static float MagnetAngle(int index) => index * Mathf.Tau / MagnetCount;

    /// <summary>Every other magnet is solid, from the first, so the solid ones are a third of a turn apart.</summary>
    public static bool IsSolid(int index) => index % 2 == 0;

    /// <summary>The centre angles of the solid magnets, which sit on a Wheel's cue tubes (<see cref="WheelPart.CueAngles"/>).</summary>
    public static float[] SolidMagnetAngles() =>
        [.. Enumerable.Range(0, MagnetCount).Where(IsSolid).Select(MagnetAngle)];

    /// <summary>The centre angle of coil <paramref name="index"/>, a sixth of a turn apart.</summary>
    public static float CoilAngle(int index) => index * Mathf.Tau / CoilCount;

    /// <summary>Magnet <paramref name="index"/> as a ring segment: its outer arc, then its inner arc back.</summary>
    public static Vector2[] MagnetShape(int index)
    {
        var centre = MagnetAngle(index);
        var outer = ArcPoints(MagnetOuterRadius, centre - MagnetHalfSpan, centre + MagnetHalfSpan);
        var inner = ArcPoints(MagnetInnerRadius, centre + MagnetHalfSpan, centre - MagnetHalfSpan);
        return [.. outer, .. inner];
    }

    /// <summary>
    /// Magnet <paramref name="index"/>'s outline as one closed stroke. It starts and ends mid-way
    /// along the outer arc, so its round ends meet on a curve, not at a corner.
    /// </summary>
    public static Vector2[] MagnetOutline(int index)
    {
        var shape = MagnetShape(index);
        var middle = _arcSegments / 2;
        return [.. shape[middle..], .. shape[..(middle + 1)]];
    }

    /// <summary>Each coil's stem, from the centre disc out, and its curved cap, as strokes.</summary>
    public static Vector2[][] CoilStrokes()
    {
        var strokes = new List<Vector2[]>(CoilCount * 2);
        for (var index = 0; index < CoilCount; index++)
        {
            var along = CoilAngle(index);
            strokes.Add([Vector2.FromAngle(along) * CoilInnerRadius, Vector2.FromAngle(along) * CoilOuterRadius]);
            strokes.Add(ArcPoints(CoilOuterRadius, along - CoilHalfSpan, along + CoilHalfSpan));
        }

        return [.. strokes];
    }

    private static Vector2[] ArcPoints(float radius, float from, float to) =>
        [.. Enumerable.Range(0, _arcSegments + 1).Select(step => Vector2.FromAngle(Mathf.Lerp(from, to, step / (float)_arcSegments)) * radius)];
}
