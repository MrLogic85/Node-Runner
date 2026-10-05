using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>A Servo (#452): a motor housing, range band and horn on a larger joint ring.</summary>
public partial class ServoPart : PartVisual
{
    private const float _housingHalfWidth = 11f;
    private const float _housingMaxReach = 8f;
    private const float _housingMinReach = 3f;
    private const float _housingCornerRadius = 2.5f;
    private const float _bandInnerRadius = 12f;
    private const float _bandOuterRadius = 20f;
    private const float _hornLong = (float)ServoDef.JointRadius - 4f;
    private const float _hornShort = 8f;
    private const int _ringSegments = 64;

    private float _radius = (float)ServoDef.JointRadius;
    private float _targetAngle;
    private float _builtAngle;
    private float _range = (float)ServoDef.DefaultRange;
    private float _start = (float)ServoDef.DefaultStart;
    private float _housingReach;
    private bool _hasFixed = true;
    private bool _hasTarget = true;
    private bool _simplified;

    public ServoPart()
        : base(CreatureLayers.Joints, CreatureLayers.SelectedJoints)
    {
    }

    public float Radius
    {
        get => _radius;
        set => Change(ref _radius, value);
    }

    /// <summary>The live Target direction, in the Servo's local frame where Fixed points along +x.</summary>
    public float TargetAngle
    {
        get => _targetAngle;
        set => Change(ref _targetAngle, value);
    }

    /// <summary>The built Target direction, in the Servo's local frame where Fixed points along +x.</summary>
    public float BuiltAngle
    {
        get => _builtAngle;
        set => Change(ref _builtAngle, value);
    }

    /// <summary>The total angular range between the stops, in radians.</summary>
    public float Range
    {
        get => _range;
        set => Change(ref _range, value);
    }

    /// <summary>Where the built angle sits in the range, 0 at the lower end and 1 at the upper.</summary>
    public float Start
    {
        get => _start;
        set => Change(ref _start, value);
    }

    /// <summary>How far the Fixed-link housing reaches past the ring.</summary>
    public float HousingReach
    {
        get => _housingReach;
        set => Change(ref _housingReach, value);
    }

    public bool HasFixed
    {
        get => _hasFixed;
        set => Change(ref _hasFixed, value);
    }

    public bool HasTarget
    {
        get => _hasTarget;
        set => Change(ref _hasTarget, value);
    }

    /// <summary>The four-primitives outline used by Training shadows.</summary>
    public bool Simplified
    {
        get => _simplified;
        set => Change(ref _simplified, value);
    }

    /// <summary>
    /// How far the housing may reach past the Servo ring without crowding a middle sensor or a
    /// Piston cylinder.
    /// </summary>
    public static float HousingReachFor(float freeLength, float sensorLength = 0, bool pistonCylinderEnd = false)
    {
        if (pistonCylinderEnd)
        {
            return 0;
        }

        var reach = MathF.Min(_housingMaxReach, ((freeLength - sensorLength) / 2) - 2);
        return reach >= _housingMinReach ? reach : 0;
    }

    public override void _Draw()
    {
        var toPixels = UiPixelSpace.Enter(this, Transform2D.Identity);
        var scale = UiPixelSpace.ScaleOf(toPixels);
        var missing = !HasFixed || !HasTarget;
        var showHousing = HasFixed && HousingReach > 0;
        var ring = missing ? Theme.Danger : Theme.MotorAccent;

        if (Simplified)
        {
            DrawSimplified(toPixels, scale, ring, showHousing, missing);
            DrawSetTransformMatrix(Transform2D.Identity);
            return;
        }

        if (showHousing)
        {
            DrawHousing(toPixels, scale, filled: true);
        }

        var at = toPixels.Origin;
        DrawCircle(at, Radius * scale, missing ? Theme.DangerFill : Selected ? Theme.SelectionFill : Theme.JointFill, antialiased: true);
        DrawArc(at, (Radius - (Theme.JointRingWidth / 2)) * scale, 0, Mathf.Tau, _ringSegments, ring, Theme.JointRingWidth * scale, antialiased: true);
        if (!missing)
        {
            DrawRangeBand(toPixels, scale, filled: true);
        }

        if (HasTarget)
        {
            DrawHorn(toPixels, scale);
        }

        if (Selected)
        {
            DrawSelection(toPixels, scale, showHousing);
        }

        if (missing)
        {
            DrawDangerBadge(toPixels, scale);
        }

        DrawSetTransformMatrix(Transform2D.Identity);
    }

    private void DrawSimplified(Transform2D toPixels, float scale, Color ring, bool showHousing, bool missing)
    {
        if (showHousing)
        {
            DrawHousing(toPixels, scale, filled: false);
        }

        DrawArc(toPixels.Origin, (Radius - (Theme.JointRingWidth / 2)) * scale, 0, Mathf.Tau, _ringSegments, ring, Theme.JointRingWidth * scale, antialiased: true);
        if (missing)
        {
            return;
        }

        DrawArcInLocal(toPixels, scale, Vector2.Zero, (_bandInnerRadius + _bandOuterRadius) / 2, LowerStop(), UpperStop(), 32, Theme.MotorAccent, 3);
        DrawLine(toPixels * Vector2.Zero, toPixels * (Vector2.FromAngle(TargetAngle) * _hornLong), Theme.MotorAccent, 3 * scale, antialiased: true);
    }

    private void DrawHousing(Transform2D toPixels, float scale, bool filled)
    {
        var (fill, outline) = HousingPoints();
        if (filled)
        {
            DrawColoredPolygon(Map(toPixels, fill), Theme.ServoPanelFill);
        }

        DrawPolyline(Map(toPixels, outline), Theme.MotorAccent, Theme.MotorSignalWidth * scale, antialiased: true);
        if (!filled || HousingReach < 6)
        {
            return;
        }

        foreach (var y in new[] { -6f, 6f })
        {
            DrawArcInLocal(toPixels, scale, new Vector2(Radius + (HousingReach / 2), y), 1.4f, 0, Mathf.Tau, 12, Theme.MotorAccent, UiSize.Stroke.Hair);
        }
    }

    private (Vector2[] Fill, Vector2[] Outline) HousingPoints()
    {
        var innerRadius = MathF.Max(Radius - 1, _housingHalfWidth + 0.1f);
        var angle = MathF.Asin(_housingHalfWidth / innerRadius);
        var end = Radius + HousingReach;
        var corner1 = RoundedCorner(new Vector2(end - _housingCornerRadius, -_housingHalfWidth + _housingCornerRadius), -Mathf.Pi / 2, 0);
        var corner2 = RoundedCorner(new Vector2(end - _housingCornerRadius, _housingHalfWidth - _housingCornerRadius), 0, Mathf.Pi / 2);
        var outline = new List<Vector2> { Vector2.FromAngle(-angle) * innerRadius };
        outline.AddRange(corner1);
        outline.AddRange(corner2);
        outline.Add(Vector2.FromAngle(angle) * innerRadius);

        var fill = new List<Vector2>(outline);
        for (var index = 1; index < 8; index++)
        {
            fill.Add(Vector2.FromAngle(angle - (2 * angle * index / 8)) * innerRadius);
        }

        return ([.. fill], [.. outline]);
    }

    private static IEnumerable<Vector2> RoundedCorner(Vector2 centre, float start, float end)
    {
        for (var index = 0; index <= 4; index++)
        {
            var angle = Mathf.Lerp(start, end, index / 4f);
            yield return centre + (Vector2.FromAngle(angle) * _housingCornerRadius);
        }
    }

    private void DrawRangeBand(Transform2D toPixels, float scale, bool filled)
    {
        var lower = LowerStop();
        var upper = UpperStop();
        if (Range >= Mathf.Tau - 0.0001f)
        {
            if (filled)
            {
                DrawArcInLocal(toPixels, scale, Vector2.Zero, (_bandInnerRadius + _bandOuterRadius) / 2, lower, lower + Mathf.Tau, _ringSegments, Theme.ServoPanelFill, _bandOuterRadius - _bandInnerRadius);
            }

            DrawArcInLocal(toPixels, scale, Vector2.Zero, _bandInnerRadius, lower, lower + Mathf.Tau, _ringSegments, Theme.MotorAccent, Theme.MotorSignalWidth);
            DrawArcInLocal(toPixels, scale, Vector2.Zero, _bandOuterRadius, lower, lower + Mathf.Tau, _ringSegments, Theme.MotorAccent, Theme.MotorSignalWidth);
            return;
        }

        var points = RangeBandPoints(lower, upper);
        if (filled)
        {
            DrawColoredPolygon(Map(toPixels, points), Theme.ServoPanelFill);
        }

        DrawPolyline(Map(toPixels, Closed(points)), Theme.MotorAccent, Theme.MotorSignalWidth * scale, antialiased: true);
    }

    private Vector2[] RangeBandPoints(float lower, float upper)
    {
        var span = upper - lower;
        var steps = Math.Max(8, Mathf.CeilToInt(span / Mathf.Tau * 72));
        var points = new List<Vector2>((steps + 1) * 2);
        for (var index = 0; index <= steps; index++)
        {
            points.Add(Vector2.FromAngle(lower + (span * index / steps)) * _bandOuterRadius);
        }

        for (var index = steps; index >= 0; index--)
        {
            points.Add(Vector2.FromAngle(lower + (span * index / steps)) * _bandInnerRadius);
        }

        return [.. points];
    }

    private void DrawHorn(Transform2D toPixels, float scale)
    {
        var along = Vector2.FromAngle(TargetAngle);
        var across = along.Orthogonal();
        var points = new[]
        {
            across * 5,
            (along * _hornLong) + (across * 3),
            (along * _hornLong) - (across * 3),
            -across * 5,
            (-along * _hornShort) - (across * 3.5f),
            (-along * _hornShort) + (across * 3.5f),
        };
        DrawColoredPolygon(Map(toPixels, points), Theme.ServoPanelFill);
        DrawPolyline(Map(toPixels, Closed(points)), Theme.MotorAccent, Theme.MotorSignalWidth * scale, antialiased: true);
        DrawCircle(toPixels.Origin, 3.5f * scale, Theme.MotorAccent, antialiased: true);
    }

    private void DrawSelection(Transform2D toPixels, float scale, bool showHousing)
    {
        var width = Theme.SelectionRingWidth * scale;
        var haloRadius = Radius + 3;
        if (!showHousing)
        {
            DrawArc(toPixels.Origin, haloRadius * scale, 0, Mathf.Tau, _ringSegments, Theme.SelectionGlow, width, antialiased: true);
            return;
        }

        var offset = _housingHalfWidth + 3;
        var gap = MathF.Asin(offset / haloRadius);
        DrawArcInLocal(toPixels, scale, Vector2.Zero, haloRadius, gap, Mathf.Tau - gap, _ringSegments, Theme.SelectionGlow, Theme.SelectionRingWidth);

        var x0 = MathF.Sqrt((haloRadius * haloRadius) - (offset * offset));
        var x1 = Radius + HousingReach + 3;
        foreach (var y in new[] { -offset, offset })
        {
            DrawLine(toPixels * new Vector2(x0, y), toPixels * new Vector2(x1, y), Theme.SelectionGlow, width, antialiased: true);
        }

        DrawLine(toPixels * new Vector2(x1, -offset - 1), toPixels * new Vector2(x1, offset + 1), Theme.SelectionGlow, width, antialiased: true);
    }

    private void DrawDangerBadge(Transform2D toPixels, float scale)
    {
        var badge = toPixels.Origin + (Vector2.FromAngle(-Mathf.Pi / 4) * Radius * scale);
        DrawCircle(badge, 8 * scale, Theme.ArenaBackground, antialiased: true);
        DrawCircle(badge, 7 * scale, Theme.Danger, antialiased: true);
        DrawLine(badge + new Vector2(0, -4.2f * scale), badge + new Vector2(0, 1.2f * scale), Theme.ArenaBackground, 2 * scale, antialiased: true);
        DrawCircle(badge + new Vector2(0, 3.6f * scale), 1.2f * scale, Theme.ArenaBackground, antialiased: true);
    }

    private float LowerStop() => BuiltAngle - (Start * Range);

    private float UpperStop() => LowerStop() + Range;

    private void DrawArcInLocal(Transform2D toPixels, float scale, Vector2 centre, float radius, float start, float end, int segments, Color color, float width) =>
        DrawArc(toPixels * centre, radius * scale, start + toPixels.Rotation, end + toPixels.Rotation, segments, color, width * scale, antialiased: true);

    private static Vector2[] Closed(Vector2[] points) => [.. points, points[0]];

    private static Vector2[] Map(Transform2D transform, IEnumerable<Vector2> points) =>
        [.. points.Select(point => transform * point)];
}
