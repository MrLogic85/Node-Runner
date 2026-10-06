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

    /// <summary>The few-primitives outline used by Training shadows.</summary>
    public bool Simplified
    {
        get => _simplified;
        set => Change(ref _simplified, value);
    }

    /// <summary>
    /// How far the housing may reach past the Servo ring without crowding a middle sensor. On a
    /// Piston it draws over the cylinder, as on any other link (#925).
    /// </summary>
    public static float HousingReachFor(float freeLength, float sensorLength = 0)
    {
        var reach = MathF.Min(_housingMaxReach, ((freeLength - sensorLength) / 2) - 2);
        return reach >= _housingMinReach ? reach : 0;
    }

    public override void _Draw()
    {
        using var pen = UiPixelPen.Begin(this);
        var missing = !HasFixed || !HasTarget;
        var showHousing = HasFixed && HousingReach > 0;
        var ring = missing ? Theme.Danger : Theme.MotorAccent;

        if (Simplified)
        {
            DrawSimplified(pen, ring, showHousing, missing);
            return;
        }

        if (showHousing)
        {
            DrawHousing(pen, filled: true);
        }

        pen.Disc(Vector2.Zero, Radius, missing ? Theme.DangerFill : Selected ? Theme.SelectionFill : Theme.JointFill);
        pen.Ring(Vector2.Zero, Radius - (Theme.JointRingWidth / 2), ring, Theme.JointRingWidth, _ringSegments);
        if (!missing)
        {
            DrawRangeBand(pen, filled: true);
        }

        if (HasTarget)
        {
            DrawHorn(pen);
        }

        if (Selected)
        {
            DrawSelection(pen, showHousing);
        }

        if (missing)
        {
            DrawDangerBadge(pen);
        }
    }

    private void DrawSimplified(UiPixelPen pen, Color ring, bool showHousing, bool missing)
    {
        if (showHousing)
        {
            DrawHousing(pen, filled: false);
        }

        pen.Ring(Vector2.Zero, Radius - (Theme.JointRingWidth / 2), ring, Theme.JointRingWidth, _ringSegments);
        if (missing)
        {
            return;
        }

        pen.Arc(Vector2.Zero, (_bandInnerRadius + _bandOuterRadius) / 2, BandFrom(), BandTo(), 32, Theme.MotorAccent, 3);
        if (IsFullTurn)
        {
            DrawStop(pen, BandFrom(), 3);
        }

        pen.Line(Vector2.Zero, Vector2.FromAngle(TargetAngle) * _hornLong, Theme.MotorAccent, 3);
    }

    private void DrawHousing(UiPixelPen pen, bool filled)
    {
        var (fill, outline) = HousingPoints();
        if (filled)
        {
            pen.Polygon(fill, Theme.ServoPanelFill);
        }

        pen.Polyline(outline, Theme.MotorAccent, Theme.MotorSignalWidth);
        if (!filled || HousingReach < 6)
        {
            return;
        }

        foreach (var y in new[] { -6f, 6f })
        {
            pen.Ring(new Vector2(Radius + (HousingReach / 2), y), 1.4f, Theme.MotorAccent, UiSize.Stroke.Hair, 12);
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

    private void DrawRangeBand(UiPixelPen pen, bool filled)
    {
        var lower = BandFrom();
        var upper = BandTo();
        if (IsFullTurn)
        {
            if (filled)
            {
                pen.Arc(Vector2.Zero, (_bandInnerRadius + _bandOuterRadius) / 2, lower, lower + Mathf.Tau, _ringSegments, Theme.ServoPanelFill, _bandOuterRadius - _bandInnerRadius);
            }

            pen.Arc(Vector2.Zero, _bandInnerRadius, lower, lower + Mathf.Tau, _ringSegments, Theme.MotorAccent, Theme.MotorSignalWidth);
            pen.Arc(Vector2.Zero, _bandOuterRadius, lower, lower + Mathf.Tau, _ringSegments, Theme.MotorAccent, Theme.MotorSignalWidth);
            DrawStop(pen, lower, Theme.MotorSignalWidth);
            return;
        }

        var points = RangeBandPoints(lower, upper);
        if (filled)
        {
            pen.Polygon(points, Theme.ServoPanelFill);
        }

        pen.Polyline(Closed(points), Theme.MotorAccent, Theme.MotorSignalWidth);
    }

    private bool IsFullTurn => Range >= Mathf.Tau - 0.0001f;

    /// <summary>
    /// A full turn's two stops meet at one angle, so a line across the band marks where it is.
    /// </summary>
    private void DrawStop(UiPixelPen pen, float angle, float width)
    {
        var along = Vector2.FromAngle(angle);
        pen.Line(along * _bandInnerRadius, along * _bandOuterRadius, Theme.MotorAccent, width);
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

    private void DrawHorn(UiPixelPen pen)
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
        pen.Polygon(points, Theme.ServoPanelFill);
        pen.Polyline(Closed(points), Theme.MotorAccent, Theme.MotorSignalWidth);
        pen.Disc(Vector2.Zero, 3.5f, Theme.MotorAccent);
    }

    private void DrawSelection(UiPixelPen pen, bool showHousing)
    {
        var width = Theme.SelectionRingWidth;
        var haloRadius = Radius + 3;
        if (!showHousing)
        {
            pen.Ring(Vector2.Zero, haloRadius, Theme.SelectionGlow, width, _ringSegments);
            return;
        }

        var offset = _housingHalfWidth + 3;
        var gap = MathF.Asin(offset / haloRadius);
        pen.Arc(Vector2.Zero, haloRadius, gap, Mathf.Tau - gap, _ringSegments, Theme.SelectionGlow, width);

        var x0 = MathF.Sqrt((haloRadius * haloRadius) - (offset * offset));
        var x1 = Radius + HousingReach + 3;
        foreach (var y in new[] { -offset, offset })
        {
            pen.Line(new Vector2(x0, y), new Vector2(x1, y), Theme.SelectionGlow, width);
        }

        pen.Line(new Vector2(x1, -offset - 1), new Vector2(x1, offset + 1), Theme.SelectionGlow, width);
    }

    // The badge keeps to the screen's upper right and stays upright however the Servo turns.
    private void DrawDangerBadge(UiPixelPen pen)
    {
        var turn = pen.ToPixels.Rotation;
        var up = Vector2.Up.Rotated(-turn);
        var badge = Vector2.FromAngle((-Mathf.Pi / 4) - turn) * Radius;
        pen.Disc(badge, 8, Theme.ArenaBackground);
        pen.Disc(badge, 7, Theme.Danger);
        pen.Line(badge + (up * 4.2f), badge - (up * 1.2f), Theme.ArenaBackground, 2);
        pen.Disc(badge - (up * 3.6f), 1.2f, Theme.ArenaBackground);
    }

    /// <summary>
    /// The range band as a clockwise sweep in Godot rotation. Model angles are counter-clockwise, so
    /// the model's upper stop is where the sweep starts and its lower stop, Start × Range past the
    /// built angle, is where it ends.
    /// </summary>
    public static (float From, float To) BandSweep(float builtAngle, float range, float start) =>
        (builtAngle - ((1 - start) * range), builtAngle + (start * range));

    private float BandFrom() => BandSweep(BuiltAngle, Range, Start).From;

    private float BandTo() => BandSweep(BuiltAngle, Range, Start).To;

    private static Vector2[] Closed(Vector2[] points) => [.. points, points[0]];
}
