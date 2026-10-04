using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// A creature's body drawn to fit its rectangle, as on a Creations card: beams, Pistons and nodes.
/// Colours are read from the Theme while drawing, so a theme swap redraws it. Its top corners
/// round to the card's, because it sits at the top of a flush card.
/// </summary>
[Tool]
[GlobalClass]
public partial class CreatureThumbnail : Control
{
    private const float _inset = UiSize.Space.S3;
    private const float _fill = 0.74f;
    private const float _nodeRadius = 5.5f;
    private const int _ringPoints = 24;

    private CreatureDef? _creature;

    public CreatureDef? Creature
    {
        get => _creature;
        set
        {
            _creature = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        UiCorners.Top(UiSize.Radius.Large).Fill(this, new Rect2(Vector2.Zero, Size), UiThemeLookup.Color(this, UiTokens.Color.Background));
        if (_creature is null || _creature.Nodes.Count == 0)
        {
            return;
        }

        var points = _creature.Nodes.Select(node => new Vector2((float)node.Position.X, (float)node.Position.Y)).ToArray();
        var min = points.Aggregate((a, b) => new Vector2(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y)));
        var max = points.Aggregate((a, b) => new Vector2(Mathf.Max(a.X, b.X), Mathf.Max(a.Y, b.Y)));
        var content = new Rect2(Vector2.One * _inset, Size - (Vector2.One * _inset * 2));
        var span = new Vector2(Mathf.Max(1, max.X - min.X), Mathf.Max(1, max.Y - min.Y));
        var scale = Mathf.Min(content.Size.X / span.X, content.Size.Y / span.Y) * _fill;
        var offset = content.GetCenter() - ((min + max) * 0.5f * scale);

        Vector2 MapNode(int nodeId) => (points[_creature.NodeIndexOf(nodeId)] * scale) + offset;
        Vector2 MapIndex(int index) => (points[index] * scale) + offset;

        var line = UiThemeLookup.Color(this, UiTokens.Color.LineStrong);
        using var pen = UiPixelPen.Begin(this);
        foreach (var beam in _creature.Beams)
        {
            pen.Line(MapNode(beam.NodeA), MapNode(beam.NodeB), line, UiSize.Stroke.Beam);
        }

        // A Piston is its rod alone at this size, in accent like on the canvas (#451).
        var accent = UiThemeLookup.Color(this, UiTokens.Color.Accent);
        foreach (var piston in _creature.Pistons)
        {
            pen.Line(MapNode(piston.NodeA), MapNode(piston.NodeB), accent, UiSize.Stroke.Signal);
        }

        var fill = UiThemeLookup.Color(this, UiTokens.Color.Panel);
        for (var i = 0; i < points.Length; i++)
        {
            pen.Disc(MapIndex(i), _nodeRadius, fill);
            pen.Ring(MapIndex(i), _nodeRadius, line, UiSize.Stroke.Signal, _ringPoints);
        }
    }
}
