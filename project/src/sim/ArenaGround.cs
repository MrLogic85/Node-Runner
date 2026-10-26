using Godot;
using NodeRunner.Domain;
using NodeRunner.Theme;

namespace NodeRunner.Sim;

/// <summary>
/// The arena's ground, built from the selected map's ground along this node's line: the
/// collider every creature body stands on (layer 1) and its fill; the ruler draws the edge. The scene places the
/// line; the map decides the ground. Flat uses Godot's endless <see cref="WorldBoundaryShape2D"/>,
/// so a creature can never walk off its end; grounds with a shape come with chunks (#91).
/// </summary>
public partial class ArenaGround : StaticBody2D
{
    // The fill reaches 10 km each way and as deep, so no zoom shows an end. A run
    // has a set length, but Simulate runs until the player leaves, so a fast walker left there
    // for hours could pass the drawn end; the collision ground is endless.
    private const float _reach = 1_000_000f;

    private readonly Polygon2D _fill = new()
    {
        Polygon = [new(-_reach, 0), new(_reach, 0), new(_reach, _reach), new(-_reach, _reach)],
    };

    public void Build(MapGround ground)
    {
        ArgumentNullException.ThrowIfNull(ground);
        if (ground is not FlatGround)
        {
            throw new NotSupportedException($"The arena builds only flat ground, not a {ground.GetType().Name}.");
        }

        // Behind the scene's own children, such as the ruler drawn along the edge.
        AddBehind(new CollisionShape2D { Shape = new WorldBoundaryShape2D() }, 0);
        AddBehind(_fill, 1);
        // A Polygon2D keeps its colour rather than drawing it from the theme.
        var theme = VisualTheme.For(this);
        theme.Bind(this, () => _fill.Color = theme.GroundFill);
    }

    private void AddBehind(Node child, int index)
    {
        AddChild(child);
        MoveChild(child, index);
    }
}
