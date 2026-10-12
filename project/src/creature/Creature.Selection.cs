using Godot;
using NodeRunner.Domain;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

// Selection (#1107): what a tap selects, and each part placed in its draw group with the selection raised.
public partial class Creature
{
    /// <summary>
    /// The part a tap at <paramref name="globalPosition"/> selects: the one drawn on top there
    /// (#1107), as in Build; off every part, the first within a finger's reach; then a Wheel's
    /// disc (#1143).
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
            // A Wheel is touched only at its hub (#1086), as it is drawn over its links.
            var radius = Math.Max(tolerance, ToGodotFloat(Definition!.CoveredRadius(Definition.Nodes[nodeIndex].Id), nameof(JointMotor.Radius)));
            if (_nodeVisuals[nodeIndex].GlobalPosition.DistanceSquaredTo(globalPosition) <= radius * radius)
            {
                var nodeId = Definition!.Nodes[nodeIndex].Id;
                selection = Definition.JointMotors.FirstOrDefault(motor => motor.NodeId == nodeId) is { } motor
                    ? new CreatureElementSelection(motor.Kind, motor.Id)
                    : Definition.Wheels.FirstOrDefault(wheel => wheel.NodeId == nodeId) is { } wheel
                        ? new CreatureElementSelection(CreatureElementKind.Wheel, wheel.Id)
                        : new CreatureElementSelection(CreatureElementKind.Node, nodeId);
                return true;
            }
        }

        for (var sensorIndex = 0; sensorIndex < _sensorVisuals.Length; sensorIndex++)
        {
            var local = _sensorVisuals[sensorIndex].ToLocal(globalPosition);
            if (local.LengthSquared() <= tolerance * tolerance)
            {
                selection = new CreatureElementSelection(CreatureElementKind.Sensor, Definition!.Sensors[sensorIndex].Id);
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

        // Off every part and reach, a Wheel's face or tyre stands for the Wheel (#1143).
        if (DrawGroups.WheelDiscAt(ToDomain(globalPosition), nodeId => ToDomain(_nodeVisuals[Definition!.NodeIndexOf(nodeId)].GlobalPosition), Definition!.Wheels) is { } disc)
        {
            selection = new CreatureElementSelection(CreatureElementKind.Wheel, disc.Id);
            return true;
        }

        selection = null;
        return false;
    }

    private static Vector2D ToDomain(Vector2 position) => new(position.X, position.Y);

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
            definition.JointMotors,
            definition.Wheels,
            definition.Sensors,
            _raised);
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
            CreatureElementKind.VelocityMotor => _velocityMotorVisuals[Definition!.VelocityMotorIndexOf(selection.Id)].GlobalPosition,
            CreatureElementKind.Piston => Middle(_pistons[Definition!.PistonIndexOf(selection.Id)].NodeA, _pistons[Definition.PistonIndexOf(selection.Id)].NodeB),
            CreatureElementKind.Spring => Middle(_springVisuals[Definition!.SpringIndexOf(selection.Id)].NodeA, _springVisuals[Definition.SpringIndexOf(selection.Id)].NodeB),
            CreatureElementKind.Wheel => _wheelVisuals[Definition!.WheelIndexOf(selection.Id)].GlobalPosition,
            CreatureElementKind.TouchSensor => _nodeVisuals[Definition!.NodeIndexOf(Definition.TouchSensors[Definition.TouchSensorIndexOf(selection.Id)].NodeId)].GlobalPosition,
            CreatureElementKind.Rubber => _nodeVisuals[Definition!.NodeIndexOf(Definition.Rubbers[Definition.RubberIndexOf(selection.Id)].NodeId)].GlobalPosition,
            _ => throw new ArgumentOutOfRangeException(nameof(selection), selection.Kind, "Not a part of a creature."),
        };

        static Vector2 Middle(Node2D a, Node2D b) => (a.GlobalPosition + b.GlobalPosition) / 2;
    }

    /// <summary>Marks the <paramref name="parts"/> selected, a stacked joint's all at once (#1066); none with an empty list.</summary>
    public void SetSelectedParts(IReadOnlyList<CreatureElementSelection> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        _selection = parts;
        ApplySelection();
    }

    // A shadow never shows a selection (#385), so its parts stay on their unselected layers.
    private void ApplySelection()
    {
        foreach (var visual in _nodeVisuals.Concat<PartVisual>(_beamVisuals).Concat(_sensorVisuals).Concat(_servoVisuals).Concat(_velocityMotorVisuals).Concat(_pistonVisuals).Concat(_springVisuals))
        {
            visual.Selected = false;
        }

        foreach (var visual in _nodeVisuals)
        {
            visual.TouchSelected = false;
            visual.WheelSelected = false;
            visual.SkinSelected = false;
        }

        var selection = _isShadow ? [] : _selection;
        ApplyDrawGroups(selection);
        foreach (var part in selection)
        {
            // A Touch sensor's mark is its joint's ring outside the cushion (#1045); the joint's own stays unselected.
            if (part.Kind == CreatureElementKind.TouchSensor)
            {
                _nodeVisuals[Definition!.NodeIndexOf(Definition.TouchSensors[Definition.TouchSensorIndexOf(part.Id)].NodeId)].TouchSelected = true;
                continue;
            }

            // A Rubber's mark is its skin in halo (#1142), drawn by its joint.
            if (part.Kind == CreatureElementKind.Rubber)
            {
                _nodeVisuals[Definition!.NodeIndexOf(Definition.Rubbers[Definition.RubberIndexOf(part.Id)].NodeId)].SkinSelected = true;
                continue;
            }

            // A Wheel's joint draws its mark with the hub, over the links (#1045).
            if (part.Kind == CreatureElementKind.Wheel)
            {
                _nodeVisuals[Definition!.NodeIndexOf(Definition.Wheels[Definition.WheelIndexOf(part.Id)].NodeId)].WheelSelected = true;
                continue;
            }

            VisualOf(part).Selected = true;
        }
    }

    private PartVisual VisualOf(CreatureElementSelection part) =>
        part.Kind switch
        {
            CreatureElementKind.Node => _nodeVisuals[Definition!.NodeIndexOf(part.Id)],
            CreatureElementKind.Beam => _beamVisuals[Definition!.BeamIndexOf(part.Id)],
            CreatureElementKind.Sensor => _sensorVisuals[Definition!.Sensors.ToList().FindIndex(sensor => sensor.Id == part.Id)],
            CreatureElementKind.Servo => _servoVisuals[Definition!.ServoIndexOf(part.Id)],
            CreatureElementKind.VelocityMotor => _velocityMotorVisuals[Definition!.VelocityMotorIndexOf(part.Id)],
            CreatureElementKind.Piston => _pistonVisuals[Definition!.PistonIndexOf(part.Id)],
            CreatureElementKind.Spring => _springVisuals[Definition!.SpringIndexOf(part.Id)],
            _ => throw new ArgumentOutOfRangeException(nameof(part), part.Kind, "Not a part of a creature."),
        };

    // Each part in its draw group (#1107), with what belongs with the selection raised as in Build's CreatureParts.
    private void ApplyDrawGroups(IReadOnlyList<CreatureElementSelection> selection)
    {
        if (Definition is not { } definition || _drawGroups is not { } groups)
        {
            return;
        }

        _raised = groups.Raised(selection, definition.JointMotors, definition.Wheels, definition.TouchSensors, definition.Rubbers);
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

        for (var i = 0; i < _velocityMotorVisuals.Length; i++)
        {
            var id = definition.VelocityMotors[i].NodeId;
            _velocityMotorVisuals[i].Place(groups, id, _raised.Joints.Contains(id));
        }

        for (var i = 0; i < _wheelVisuals.Length; i++)
        {
            var id = definition.Wheels[i].NodeId;
            _wheelVisuals[i].Place(groups, id, _raised.Wheels.Contains(id));
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
