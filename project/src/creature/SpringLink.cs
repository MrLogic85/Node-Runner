using Godot;

namespace NodeRunner.Creature;

/// <summary>
/// A Spring's Godot DampedSpringJoint2D (#453) and its stops (#835). Each physics tick it gives the
/// joint <see cref="Mechanics.Spring.StepRestLength"/>, so a Spring preloaded past a stop pushes only
/// what reaches that stop and the stop keeps the rest of its preload inside.
/// </summary>
public sealed class SpringLink
{
    private readonly DampedSpringJoint2D _joint;
    private readonly RigidBody2D _nodeA;
    private readonly RigidBody2D _nodeB;

    // The Spring's own rest length; the joint's is the one StepRestLength gives it this tick.
    private readonly double _restLength;
    private readonly double _shortest;
    private readonly double _longest;
    private readonly double _reducedMass;

    public SpringLink(DampedSpringJoint2D joint, RigidBody2D nodeA, RigidBody2D nodeB, double restLength, double shortest, double longest)
    {
        _joint = joint;
        _nodeA = nodeA;
        _nodeB = nodeB;
        _restLength = restLength;
        _shortest = shortest;
        _longest = longest;
        _reducedMass = nodeA.Mass * nodeB.Mass / (nodeA.Mass + nodeB.Mass);
    }

    /// <param name="step">The physics step, in seconds.</param>
    public void Step(double step)
    {
        var between = _nodeB.GlobalPosition - _nodeA.GlobalPosition;
        var length = between.Length();
        var speed = (_nodeB.LinearVelocity - _nodeA.LinearVelocity).Dot(between / Math.Max(length, 1e-3f));
        _joint.RestLength = (float)Mechanics.Spring.StepRestLength(
            _restLength, _shortest, _longest, _joint.Stiffness, length, speed, _reducedMass, step);
    }
}
