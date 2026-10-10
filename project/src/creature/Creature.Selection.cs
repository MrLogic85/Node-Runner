using Godot;
using NodeRunner.Domain;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

// Selection (#1107): what a tap selects, and each part placed in its draw group with the selection raised.
public partial class Creature
{
    /// <summary>
    /// The part a tap at <paramref name="globalPosition"/> selects: the one drawn on top there
    /// (#1107), as in Build; off every part, the first within a finger's reach.
    /// </summary>
    public bool TrySelectPart(Vector2 globalPosition, out CreatureElementSelection? selection)
    {
        selection = DrawnPartAt(globalPosition);
        if (selection is not null)
        {
            return true;
        }

        var tolerance = GetHitTolerance();
        for (var nodeIndex = 0; nodeIndex < _nodeVisuals.Length; nodeIndex++)
        {
            var radius = Math.Max(tolerance, ToGodotFloat(Definition!.NodeRadius(Definition.Nodes[nodeIndex].Id), nameof(ServoDef.JointRadius)));
            if (_nodeVisuals[nodeIndex].GlobalPosition.DistanceSquaredTo(globalPosition) <= radius * radius)
            {
                var nodeId = Definition!.Nodes[nodeIndex].Id;
                selection = Definition.Servos.FirstOrDefault(servo => servo.NodeId == nodeId) is { } servo
                    ? new CreatureElementSelection(CreatureElementKind.Servo, servo.Id)
                    : Definition.Wheels.FirstOrDefault(wheel => wheel.NodeId == nodeId) is { } wheel
                        ? new CreatureElementSelection(CreatureElementKind.Wheel, wheel.Id)
                        : new CreatureElementSelection(CreatureElementKind.Node, nodeId);
                return true;
            }
        }

        for (var sensorIndex = 0; sensorIndex < _sensorVisuals.Length; sensorIndex++)
        {
            var beamIndex = Definition!.BeamIndexOf(Definition.Sensors[sensorIndex].BeamId);
            var local = _beamBodies[beamIndex].ToLocal(globalPosition);
            if (local.LengthSquared() <= tolerance * tolerance)
            {
                selection = new CreatureElementSelection(CreatureElementKind.Sensor, Definition.Sensors[sensorIndex].Id);
                return true;
            }
        }

        for (var pistonIndex = 0; pistonIndex < _pistons.Length; pistonIndex++)
        {
            var piston = _pistons[pistonIndex];
            if (DistanceSquaredToSegment(globalPosition, piston.NodeA.GlobalPosition, piston.NodeB.GlobalPosition) <= tolerance * tolerance)
            {
                selection = new CreatureElementSelection(CreatureElementKind.Piston, piston.Definition.Id);
                return true;
            }
        }

        for (var springIndex = 0; springIndex < _springVisuals.Length; springIndex++)
        {
            var spring = _springVisuals[springIndex];
            if (DistanceSquaredToSegment(globalPosition, spring.NodeA.GlobalPosition, spring.NodeB.GlobalPosition) <= tolerance * tolerance)
            {
                selection = new CreatureElementSelection(CreatureElementKind.Spring, Definition!.Springs[springIndex].Id);
                return true;
            }
        }

        for (var beamIndex = 0; beamIndex < _beamBodies.Length; beamIndex++)
        {
            var body = _beamBodies[beamIndex];
            var halfLength = _beamHalfLengths[beamIndex];
            var start = body.ToGlobal(new Vector2(-halfLength, 0));
            var end = body.ToGlobal(new Vector2(halfLength, 0));
            if (DistanceSquaredToSegment(globalPosition, start, end) <= tolerance * tolerance)
            {
                selection = new CreatureElementSelection(CreatureElementKind.Beam, Definition!.Beams[beamIndex].Id);
                return true;
            }
        }

        selection = null;
        return false;
    }

    // The part drawn on top at the point (#1107), by the rule Build's touches follow too.
    private CreatureElementSelection? DrawnPartAt(Vector2 globalPosition)
    {
        if (Definition is not { } definition || _drawGroups is not { } groups)
        {
            return null;
        }

        return groups.PartAt(
            ToDomain(globalPosition),
            nodeId => ToDomain(_nodeVisuals[definition.NodeIndexOf(nodeId)].GlobalPosition),
            definition.Servos,
            definition.Wheels,
            definition.Sensors,
            _raised);

        static Vector2D ToDomain(Vector2 position) => new(position.X, position.Y);
    }

    /// <summary>
    /// Where a part's name points to, in global coordinates (#388): a joint's centre, a beam's or
    /// link's middle, or a sensor's picture.
    /// </summary>
    public Vector2 PartAnchor(CreatureElementSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return selection.Kind switch
        {
            CreatureElementKind.Node => _nodeVisuals[Definition!.NodeIndexOf(selection.Id)].GlobalPosition,
            CreatureElementKind.Beam => _beamBodies[Definition!.BeamIndexOf(selection.Id)].GlobalPosition,
            CreatureElementKind.Sensor => _sensorVisuals[Definition!.Sensors.ToList().FindIndex(sensor => sensor.Id == selection.Id)].GlobalPosition,
            CreatureElementKind.Servo => _servoVisuals[Definition!.ServoIndexOf(selection.Id)].GlobalPosition,
            CreatureElementKind.Piston => Middle(_pistons[Definition!.PistonIndexOf(selection.Id)].NodeA, _pistons[Definition.PistonIndexOf(selection.Id)].NodeB),
            CreatureElementKind.Spring => Middle(_springVisuals[Definition!.SpringIndexOf(selection.Id)].NodeA, _springVisuals[Definition.SpringIndexOf(selection.Id)].NodeB),
            CreatureElementKind.Wheel => _wheelVisuals[Definition!.WheelIndexOf(selection.Id)].GlobalPosition,
            _ => throw new ArgumentOutOfRangeException(nameof(selection), selection.Kind, "Not a part of a creature."),
        };

        static Vector2 Middle(Node2D a, Node2D b) => (a.GlobalPosition + b.GlobalPosition) / 2;
    }

    public void SetSelectedElement(CreatureElementSelection? selection)
    {
        _selection = selection;
        ApplySelection();
    }

    // A shadow never shows a selection (#385), so its parts stay on their unselected layers.
    private void ApplySelection()
    {
        foreach (var visual in _nodeVisuals.Concat<PartVisual>(_beamVisuals).Concat(_sensorVisuals).Concat(_servoVisuals).Concat(_pistonVisuals).Concat(_springVisuals).Concat(_wheelVisuals))
        {
            visual.Selected = false;
        }

        ApplyDrawGroups(_isShadow ? null : _selection);
        if (_selection is null || _isShadow)
        {
            return;
        }

        PartVisual selected = _selection.Kind switch
        {
            CreatureElementKind.Node => _nodeVisuals[Definition!.NodeIndexOf(_selection.Id)],
            CreatureElementKind.Beam => _beamVisuals[Definition!.BeamIndexOf(_selection.Id)],
            CreatureElementKind.Sensor => _sensorVisuals[Definition!.Sensors.ToList().FindIndex(sensor => sensor.Id == _selection.Id)],
            CreatureElementKind.Servo => _servoVisuals[Definition!.ServoIndexOf(_selection.Id)],
            CreatureElementKind.Piston => _pistonVisuals[Definition!.PistonIndexOf(_selection.Id)],
            CreatureElementKind.Spring => _springVisuals[Definition!.SpringIndexOf(_selection.Id)],
            CreatureElementKind.Wheel => _wheelVisuals[Definition!.WheelIndexOf(_selection.Id)],
            _ => throw new ArgumentOutOfRangeException(nameof(_selection), _selection.Kind, "Not a part of a creature."),
        };
        selected.Selected = true;
    }

    // Each part in its draw group (#1107), with what belongs with the selection raised as in Build's CreatureParts.
    private void ApplyDrawGroups(CreatureElementSelection? selection)
    {
        if (Definition is not { } definition || _drawGroups is not { } groups)
        {
            return;
        }

        _raised = groups.Raised(selection is null ? [] : [selection], definition.Servos, definition.Wheels);
        for (var i = 0; i < _nodeVisuals.Length; i++)
        {
            var id = definition.Nodes[i].Id;
            _nodeVisuals[i].Place(groups, id, _raised.Joints.Contains(id));
        }

        for (var i = 0; i < _servoVisuals.Length; i++)
        {
            var id = definition.Servos[i].NodeId;
            _servoVisuals[i].Place(groups, id, _raised.Joints.Contains(id));
        }

        for (var i = 0; i < _wheelVisuals.Length; i++)
        {
            var id = definition.Wheels[i].NodeId;
            _wheelVisuals[i].Place(groups, id, _raised.Joints.Contains(id));
        }

        for (var i = 0; i < _beamVisuals.Length; i++)
        {
            var id = definition.Beams[i].Id;
            _beamVisuals[i].Place(groups, id, _raised.Links.Contains(id));
        }

        for (var i = 0; i < _pistonVisuals.Length; i++)
        {
            var id = definition.Pistons[i].Id;
            _pistonVisuals[i].Place(groups, id, _raised.Links.Contains(id));
        }

        for (var i = 0; i < _springVisuals.Length; i++)
        {
            var id = definition.Springs[i].Id;
            _springVisuals[i].Place(groups, id, _raised.Links.Contains(id));
        }

        for (var i = 0; i < _sensorVisuals.Length; i++)
        {
            _sensorVisuals[i].Place(groups, definition.Sensors[i].BeamId, _raised.Sensor(definition.Sensors[i]));
        }
    }
}
