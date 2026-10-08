using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// Enforces <c>docs/UI_DIRECTION.md</c> → "Icon files" (#1029) and "Icon drawing" (#1010, #1018): a file's
/// prefix names its kind, and the kind sets its grid and drawn size. Bounds are the geometry alone, which holds because every
/// UI icon and mark shares one stroke, untransformed.
/// </summary>
public sealed partial class UiIconGridTests
{
    private const double _centre = 12;
    private const double _tolerance = 0.1;

    // A null drawn size leaves the drawing free: part glyphs only keep their 20 grid.
    private static readonly (string Prefix, string ViewBox, double? Drawn)[] _kinds =
    [
        (UiIcons.IconPrefix, "0 0 24 24", 16),
        (UiIcons.MarkPrefix, "0 0 24 24", 20),
        (UiIcons.PartPrefix, "0 0 20 20", null),
    ];

    [Fact]
    public void EveryIcon_IsNamedByItsKind_AndDrawnOnThatKindsGrid()
    {
        var folder = Path.Combine(SceneNodes.FindRepositoryRoot(), "project", "assets", "icons");
        var launcherArt = Path.Combine(folder, "app") + Path.DirectorySeparatorChar;
        var files = Directory.GetFiles(folder, "*.svg", SearchOption.AllDirectories)
            .Where(file => !file.StartsWith(launcherArt, StringComparison.Ordinal))
            .ToList();
        files.ShouldNotBeEmpty();

        var failures = files.Select(file => Check(folder, file)).OfType<string>().ToList();

        failures.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("<path d=\"M 4,4 l 16,16 M 20,4 L 4,20\"/>", 4, 4, 20, 20)]
    [InlineData("<path d=\"M 4,12 a 8,8 0 1,0 16,0 a 8,8 0 1,0 -16,0 z\"/>", 4, 4, 20, 20)]
    [InlineData("<path d=\"M 4,20 Q 12,4 20,20\"/>", 4, 12, 20, 20)]
    [InlineData("<path d=\"M 4,8 H 20 V 16\"/>", 4, 8, 20, 16)]
    [InlineData("<circle cx=\"12\" cy=\"12\" r=\"8\"/>", 4, 4, 20, 20)]
    [InlineData("<rect x=\"4\" y=\"6\" width=\"16\" height=\"12\"/>", 4, 6, 20, 18)]
    public void Bounds_FollowTheDrawnGeometry(string shape, double left, double top, double right, double bottom)
    {
        var bounds = Bounds(Svg(shape));

        bounds.Left.ShouldBe(left, _tolerance);
        bounds.Top.ShouldBe(top, _tolerance);
        bounds.Right.ShouldBe(right, _tolerance);
        bounds.Bottom.ShouldBe(bottom, _tolerance);
    }

    [Fact]
    public void AShapeTheBoundsCannotRead_IsRejected()
    {
        Should.Throw<NotSupportedException>(() => Bounds(Svg("<ellipse cx=\"12\" cy=\"12\" rx=\"8\" ry=\"4\"/>")));
    }

    [Theory]
    [InlineData("stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"", "<path d=\"M 4,4 L 20,20\"/>", false)]
    [InlineData("stroke-width=\"3\" stroke-linecap=\"round\" stroke-linejoin=\"round\"", "<path d=\"M 4,4 L 20,20\"/>", true)]
    [InlineData("stroke-width=\"2\" stroke-linecap=\"round\"", "<path d=\"M 4,4 L 20,20\"/>", true)]
    [InlineData("stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"", "<path stroke-width=\"3\" d=\"M 4,4 L 20,20\"/>", true)]
    [InlineData("stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"", "<path transform=\"scale(2)\" d=\"M 4,4 L 20,20\"/>", true)]
    public void AnIconOffTheSharedStroke_IsCaught(string rootStroke, string shape, bool caught)
    {
        var svg = XElement.Parse($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" {rootStroke}>{shape}</svg>");

        (StrokeMismatch(svg) is not null).ShouldBe(caught);
    }

    private static XElement Svg(string shape) =>
        XElement.Parse($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\">{shape}</svg>");

    private static string? Check(string folder, string file)
    {
        var name = Path.GetFileName(file);
        if (Path.GetDirectoryName(file) != folder)
        {
            return $"{Path.GetRelativePath(folder, file)}: is in a subfolder; icons share one folder";
        }

        var kind = _kinds.FirstOrDefault(entry => name.StartsWith(entry.Prefix, StringComparison.Ordinal));
        if (kind.Prefix is null)
        {
            return $"{name}: has no kind prefix ({string.Join(", ", _kinds.Select(entry => entry.Prefix))})";
        }

        if (!SnakeCase().IsMatch(name))
        {
            return $"{name}: is not snake_case";
        }

        var svg = XElement.Load(file);
        if ((string?)svg.Attribute("viewBox") != kind.ViewBox)
        {
            return $"{name}: viewBox is not {kind.ViewBox}";
        }

        if (kind.Drawn is not { } size)
        {
            return null;
        }

        if (StrokeMismatch(svg) is { } stroke)
        {
            return $"{name}: {stroke}";
        }

        var bounds = Bounds(svg);
        var longer = Math.Max(bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
        var centreX = (bounds.Left + bounds.Right) / 2;
        var centreY = (bounds.Top + bounds.Bottom) / 2;
        return Math.Abs(longer - size) > _tolerance || Math.Abs(centreX - _centre) > _tolerance || Math.Abs(centreY - _centre) > _tolerance
            ? $"{name}: drawing is {longer:0.##} on its longer side, centred at ({centreX:0.##}, {centreY:0.##})"
            : null;
    }

    private static readonly (string Name, string Value)[] _sharedStroke =
        [("stroke-width", "2"), ("stroke-linecap", "round"), ("stroke-linejoin", "round")];

    // The geometry-only bounds are fair only while the stroke adds the same margin to every icon.
    private static string? StrokeMismatch(XElement svg)
    {
        foreach (var (attribute, value) in _sharedStroke)
        {
            if ((string?)svg.Attribute(attribute) != value)
            {
                return $"the root's {attribute} is not {value}";
            }
        }

        foreach (var shape in svg.Descendants())
        {
            if (shape.Attribute("transform") is not null)
            {
                return $"<{shape.Name.LocalName}> has a transform";
            }

            if (_sharedStroke.FirstOrDefault(stroke => shape.Attribute(stroke.Name) is not null) is { Name: { } attribute })
            {
                return $"<{shape.Name.LocalName}> sets its own {attribute}";
            }
        }

        return null;
    }

    private static (double Left, double Top, double Right, double Bottom) Bounds(XElement svg)
    {
        var points = new List<(double X, double Y)>();
        foreach (var shape in svg.Elements())
        {
            switch (shape.Name.LocalName)
            {
                case "path":
                    AddPath(points, (string)shape.Attribute("d")!);
                    break;
                case "circle":
                    var (cx, cy, r) = (Number(shape, "cx"), Number(shape, "cy"), Number(shape, "r"));
                    points.Add((cx - r, cy - r));
                    points.Add((cx + r, cy + r));
                    break;
                case "rect":
                    var (x, y) = (Number(shape, "x"), Number(shape, "y"));
                    points.Add((x, y));
                    points.Add((x + Number(shape, "width"), y + Number(shape, "height")));
                    break;
                default:
                    throw new NotSupportedException($"<{shape.Name.LocalName}> is not read by the icon bounds");
            }
        }

        return (points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y));
    }

    private static double Number(XElement shape, string attribute) =>
        double.Parse((string?)shape.Attribute(attribute) ?? "0", CultureInfo.InvariantCulture);

    // Lines add their end points; curves and arcs add samples along them, which is exact enough
    // for a 0.1 tolerance on a 24 grid.
    private static void AddPath(List<(double X, double Y)> points, string data)
    {
        const int samples = 64;
        var tokens = PathToken().Matches(data).Select(match => match.Value).ToList();
        var (x, y, startX, startY) = (0.0, 0.0, 0.0, 0.0);
        var (controlX, controlY) = (0.0, 0.0);
        var command = 'M';
        var previous = 'M';
        var index = 0;

        double Next() => double.Parse(tokens[index++], CultureInfo.InvariantCulture);
        void Add(double px, double py) => points.Add((px, py));

        while (index < tokens.Count)
        {
            if (char.IsLetter(tokens[index][0]))
            {
                command = tokens[index++][0];
            }

            var relative = char.IsLower(command);
            var (ox, oy) = relative ? (x, y) : (0.0, 0.0);
            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                    (x, y) = (ox + Next(), oy + Next());
                    (startX, startY) = (x, y);
                    Add(x, y);
                    command = relative ? 'l' : 'L';
                    break;
                case 'L':
                    (x, y) = (ox + Next(), oy + Next());
                    Add(x, y);
                    break;
                case 'H':
                    x = ox + Next();
                    Add(x, y);
                    break;
                case 'V':
                    y = (relative ? y : 0) + Next();
                    Add(x, y);
                    break;
                case 'Z':
                    (x, y) = (startX, startY);
                    break;
                case 'Q':
                case 'T':
                {
                    var smooth = char.ToUpperInvariant(command) == 'T';
                    var (qx, qy) = smooth
                        ? ("QT".Contains(char.ToUpperInvariant(previous)) ? (2 * x - controlX, 2 * y - controlY) : (x, y))
                        : (ox + Next(), oy + Next());
                    var (ex, ey) = (ox + Next(), oy + Next());
                    for (var i = 1; i <= samples; i++)
                    {
                        var t = i / (double)samples;
                        var u = 1 - t;
                        Add(u * u * x + 2 * u * t * qx + t * t * ex, u * u * y + 2 * u * t * qy + t * t * ey);
                    }

                    (controlX, controlY, x, y) = (qx, qy, ex, ey);
                    break;
                }

                case 'C':
                case 'S':
                {
                    var smooth = char.ToUpperInvariant(command) == 'S';
                    var (c1x, c1y) = smooth
                        ? ("CS".Contains(char.ToUpperInvariant(previous)) ? (2 * x - controlX, 2 * y - controlY) : (x, y))
                        : (ox + Next(), oy + Next());
                    var (c2x, c2y) = (ox + Next(), oy + Next());
                    var (ex, ey) = (ox + Next(), oy + Next());
                    for (var i = 1; i <= samples; i++)
                    {
                        var t = i / (double)samples;
                        var u = 1 - t;
                        Add(
                            u * u * u * x + 3 * u * u * t * c1x + 3 * u * t * t * c2x + t * t * t * ex,
                            u * u * u * y + 3 * u * u * t * c1y + 3 * u * t * t * c2y + t * t * t * ey);
                    }

                    (controlX, controlY, x, y) = (c2x, c2y, ex, ey);
                    break;
                }

                case 'A':
                {
                    var (rx, ry, rotation) = (Math.Abs(Next()), Math.Abs(Next()), Next() * Math.PI / 180);
                    var (large, sweep) = (Next() != 0, Next() != 0);
                    var (ex, ey) = (ox + Next(), oy + Next());
                    AddArc(points, (x, y), (ex, ey), rx, ry, rotation, large, sweep, samples);
                    (x, y) = (ex, ey);
                    break;
                }

                default:
                    throw new NotSupportedException($"Path command '{command}' is not read by the icon bounds");
            }

            previous = command;
        }
    }

    // SVG 1.1 appendix F.6.5: endpoint to centre parameterisation.
    private static void AddArc(
        List<(double X, double Y)> points, (double X, double Y) from, (double X, double Y) to,
        double rx, double ry, double rotation, bool large, bool sweep, int samples)
    {
        if (rx == 0 || ry == 0)
        {
            points.Add(to);
            return;
        }

        var (cos, sin) = (Math.Cos(rotation), Math.Sin(rotation));
        var (dx, dy) = ((from.X - to.X) / 2, (from.Y - to.Y) / 2);
        var (x1, y1) = (cos * dx + sin * dy, -sin * dx + cos * dy);
        var scale = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
        if (scale > 1)
        {
            (rx, ry) = (rx * Math.Sqrt(scale), ry * Math.Sqrt(scale));
        }

        var numerator = Math.Max(0, rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1);
        var factor = Math.Sqrt(numerator / (rx * rx * y1 * y1 + ry * ry * x1 * x1)) * (large == sweep ? -1 : 1);
        var (cx1, cy1) = (factor * rx * y1 / ry, -factor * ry * x1 / rx);
        var (cx, cy) = (cos * cx1 - sin * cy1 + (from.X + to.X) / 2, sin * cx1 + cos * cy1 + (from.Y + to.Y) / 2);

        var start = Math.Atan2((y1 - cy1) / ry, (x1 - cx1) / rx);
        var delta = Math.Atan2((-y1 - cy1) / ry, (-x1 - cx1) / rx) - start;
        if (sweep && delta < 0)
        {
            delta += 2 * Math.PI;
        }
        else if (!sweep && delta > 0)
        {
            delta -= 2 * Math.PI;
        }

        for (var i = 1; i <= samples; i++)
        {
            var angle = start + delta * i / samples;
            var (ax, ay) = (rx * Math.Cos(angle), ry * Math.Sin(angle));
            points.Add((cos * ax - sin * ay + cx, sin * ax + cos * ay + cy));
        }
    }

    [GeneratedRegex(@"^[a-z0-9]+(_[a-z0-9]+)*\.svg$")]
    private static partial Regex SnakeCase();

    [GeneratedRegex(@"[A-Za-z]|[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?")]
    private static partial Regex PathToken();
}
