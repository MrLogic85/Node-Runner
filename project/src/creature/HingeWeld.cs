using Godot;

namespace NodeRunner.Creature;

/// <summary>
/// What welds a hinge part's two sides rigid (Creature.Links.cs, Welder): one <see cref="GrooveJoint2D"/>,
/// made with the creature, that <see cref="Weld"/> fastens to the sides and <see cref="Release"/> lets go,
/// so a Stepper (#1037) locks at every notch without making a node in the tick. A point on the held body,
/// where the far node stands, runs in a groove on the other body along the line from the joint: it can
/// slide along the line, as a Piston's or Spring's stroke does, but not turn about the joint. Godot makes
/// the joint whenever its node B is set, and clears it when there is none.
/// </summary>
public sealed class HingeWeld
{
    // How far the groove reaches each way from the joint: 100 m, far past any link or stroke, so only a
    // Piston's or Spring's end stops stop its stroke. It has no cap (CapEndStops caps only those): Godot
    // caps a groove's whole push, across it too, so a capped groove would let the lock turn.
    private const float _grooveReach = 10000;

    // How much of the weld's error Godot's solver takes back each step: its most, so a Stepper caught
    // past its notch is on it within a few ticks (#1272). Godot's default, 0.2, took a quarter second.
    // A Locked Brake's weld takes back its drift as fast; creatures with Brakes moved as before.
    private const float _weldBias = 0.9f;

    private readonly GrooveJoint2D _groove;
    private readonly RigidBody2D _grooveBody;
    private readonly RigidBody2D _heldBody;
    private readonly NodePath _held;
    private readonly NodePath _none = new();
    private readonly Node2D _joint;
    private readonly Node2D _far;
    private readonly bool _heldIsTarget;

    /// <param name="groove">The joint, in the tree, not yet holding.</param>
    /// <param name="grooveBody">The body the groove runs on (node A).</param>
    /// <param name="heldBody">The body that runs in it (node B).</param>
    /// <param name="joint">The hinge part's joint body, which the groove runs through.</param>
    /// <param name="far">The far node where the held point is.</param>
    /// <param name="heldIsTarget">Whether the held body turns with the part's target side.</param>
    public HingeWeld(GrooveJoint2D groove, RigidBody2D grooveBody, RigidBody2D heldBody, Node2D joint, Node2D far, bool heldIsTarget)
    {
        _groove = groove;
        _grooveBody = grooveBody;
        _heldBody = heldBody;
        _joint = joint;
        _far = far;
        _heldIsTarget = heldIsTarget;
        _groove.Length = 2 * _grooveReach;
        _groove.Bias = _weldBias;
        _groove.NodeA = _groove.GetPathTo(grooveBody);
        _held = _groove.GetPathTo(heldBody);
    }

    public bool Welded { get; private set; }

    /// <summary>
    /// Welds the sides where they will be once the target side has turned back by
    /// <paramref name="overshoot"/> radians, in the model's counter-clockwise-positive sense (#1272):
    /// Godot's solver turns them there. 0 welds them as they stand.
    /// </summary>
    public void Weld(double overshoot = 0)
    {
        var joint = _joint.GlobalPosition;
        var far = _far.GlobalPosition;
        var toFar = far - joint;
        var direction = Mechanics.HingePartSideGeometry.WeldGrooveDirection(
            new Domain.Vector2D(joint.X, joint.Y), new Domain.Vector2D(far.X, far.Y), overshoot, _heldIsTarget);
        var along = new Vector2((float)direction.X, (float)direction.Y);
        _groove.GlobalPosition = joint - (along * _grooveReach);
        _groove.GlobalRotation = along.Angle() - (Mathf.Pi / 2);
        _groove.InitialOffset = _grooveReach + toFar.Length();
        _groove.NodeB = _held;
        Welded = true;
        if (overshoot == 0)
        {
            return;
        }

        // Godot's GrooveJoint2D anchors node B on its groove, so it only welds the sides as they stand.
        // The server makes the same groove with the held point still at the far node, off it.
        PhysicsServer2D.JointMakeGroove(_groove.GetRid(), joint - (along * _grooveReach), joint + (along * _grooveReach), far, _grooveBody.GetRid(), _heldBody.GetRid());
    }

    public void Release()
    {
        _groove.NodeB = _none;
        Welded = false;
    }
}
