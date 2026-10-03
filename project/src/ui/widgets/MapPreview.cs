using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// A small picture of a map's ground, as on a Train setup map card (#194): one line on the arena's
/// background, the reference's map pictures. Its top corners round to the card's, because it sits
/// at the top of a flush card. The shapes are drawn here until maps have ground data of their own (#443).
/// </summary>
[Tool]
[GlobalClass]
public partial class MapPreview : Control
{
    public enum Terrain
    {
        Flat,
        Hills,
        Stairs,
    }

    // The shapes are drawn in the reference's 118 × 52 picture and stretched to the preview's size.
    private static readonly Vector2 _designSize = new(118, 52);
    private static readonly Vector2[] _flat = [new(0, 34), new(118, 34)];
    private static readonly Vector2[] _stairs =
    [
        new(0, 40), new(24, 40), new(24, 32), new(48, 32), new(48, 24),
        new(72, 24), new(72, 16), new(96, 16), new(96, 8), new(118, 8),
    ];

    // Hills are quadratic curves: the ground passes through each point, pulled toward the control between.
    private static readonly Vector2[] _hillPoints = [new(0, 34), new(30, 34), new(60, 34), new(90, 34), new(118, 30)];
    private static readonly Vector2[] _hillControls = [new(15, 18), new(45, 50), new(75, 18), new(105, 50)];

    // How far a cubic handle reaches toward a quadratic curve's control.
    private const float _quadraticToCubic = 2f / 3f;

    private Terrain _map;
    private UiTokens.Color _lineColor = UiTokens.Color.Accent;

    [Export]
    public Terrain Map
    {
        get => _map;
        set
        {
            _map = value;
            QueueRedraw();
        }
    }

    /// <summary>The ground line's colour: accent on the map in use, a quieter line on a locked one.</summary>
    [Export]
    public UiTokens.Color LineColor
    {
        get => _lineColor;
        set
        {
            _lineColor = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        UiCorners.Top(UiSize.Radius.Large).Fill(this, new Rect2(Vector2.Zero, Size), UiThemeLookup.Color(this, UiTokens.Color.Background));
        var stretch = Size / _designSize;
        var toPixels = UiPixelSpace.Enter(this, Transform2D.Identity);
        var line = Ground(_map).Select(point => toPixels * (point * stretch)).ToArray();
        DrawPolyline(line, UiThemeLookup.Color(this, _lineColor), UiSize.Stroke.Signal * UiPixelSpace.ScaleOf(toPixels), antialiased: true);
        DrawSetTransformMatrix(Transform2D.Identity);
    }

    private static Vector2[] Ground(Terrain map) => map switch
    {
        Terrain.Hills => Hills(),
        Terrain.Stairs => _stairs,
        _ => _flat,
    };

    private static Vector2[] Hills()
    {
        using var curve = new Curve2D();
        for (var i = 0; i < _hillPoints.Length; i++)
        {
            var point = _hillPoints[i];
            var handleIn = i > 0 ? (_hillControls[i - 1] - point) * _quadraticToCubic : Vector2.Zero;
            var handleOut = i < _hillControls.Length ? (_hillControls[i] - point) * _quadraticToCubic : Vector2.Zero;
            curve.AddPoint(point, handleIn, handleOut);
        }

        return curve.Tessellate();
    }
}
