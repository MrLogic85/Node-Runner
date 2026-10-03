using Godot;
using NodeRunner.Domain;
using NodeRunner.Theme;

namespace NodeRunner.Sim;

/// <summary>
/// The arena's ground, built from the selected map's ground (#444) along this node's line: the
/// collider every creature body stands on (layer 1) and its fill and edge. The scene places the
/// line; the map decides the ground. Flat uses Godot's endless <see cref="WorldBoundaryShape2D"/>,
/// so a creature can never walk off its end; grounds with a shape come with chunks (#91).
/// </summary>
public partial class ArenaGround : StaticBody2D
{
    // The fill and edge reach 10 km each way, and the fill as deep, so no zoom shows an end. A run
    // has a set length, but Simulate (#702) runs until the player leaves, so a fast walker left there
    // for hours could pass the drawn end; the collision ground is endless.
    private const float _reach = 1_000_000f;

    public void Build(MapGround ground, VisualTheme theme)
    {
        ArgumentNullException.ThrowIfNull(ground);
        ArgumentNullException.ThrowIfNull(theme);
        if (ground is not FlatGround)
        {
            throw new NotSupportedException($"The arena builds only flat ground, not a {ground.GetType().Name}.");
        }

        // Behind the scene's own children, such as the ruler drawn along the edge.
        AddBehind(new CollisionShape2D { Shape = new WorldBoundaryShape2D() }, 0);
        AddBehind(
            new Polygon2D
            {
                Polygon = [new(-_reach, 0), new(_reach, 0), new(_reach, _reach), new(-_reach, _reach)],
                Color = theme.GroundFill,
            },
            1);
        AddBehind(
            new Line2D
            {
                Points = [new(-_reach, 0), new(_reach, 0)],
                DefaultColor = theme.GroundEdge,
                Width = theme.GroundEdgeWidth,
            },
            2);
    }

    private void AddBehind(Node child, int index)
    {
        AddChild(child);
        MoveChild(child, index);
    }
}
