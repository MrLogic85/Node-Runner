using Godot;

namespace NodeRunner.Creature;

/// <summary>
/// What welds a hinge part's two sides rigid (Creature.Links.cs, Welder): one Godot joint, made with
/// the creature, that <see cref="Weld"/> fastens to the sides as they stand and <see cref="Release"/>
/// lets go, so a Stepper (#1037) locks at every notch without making a node in the tick. Godot anchors
/// a joint to its bodies' current pose whenever its second body is set, and clears it when there is none.
/// </summary>
public sealed class HingeWeld
{
    private readonly Joint2D _joint;
    private readonly NodePath _held;
    private readonly NodePath _none = new();
    private readonly Action _place;

    /// <param name="joint">The joint, in the tree, with its node A set and no node B.</param>
    /// <param name="held">The node B it fastens.</param>
    /// <param name="place">Moves the joint to where it welds the sides as they stand.</param>
    public HingeWeld(Joint2D joint, NodePath held, Action place)
    {
        _joint = joint;
        _held = held;
        _place = place;
    }

    public bool Welded { get; private set; }

    public void Weld()
    {
        _place();
        _joint.NodeB = _held;
        Welded = true;
    }

    public void Release()
    {
        _joint.NodeB = _none;
        Welded = false;
    }
}
