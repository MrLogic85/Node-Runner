using Godot;
using NodeRunner.Domain;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

// Selection: what a tap selects, and each part placed in its draw group with the selection raised.
public partial class Creature
{
    /// <summary>
    /// The part a tap at <paramref name="globalPosition"/> selects: the one drawn on top there,
    /// as in Build; off every part, the first within a finger's reach; then a Wheel's disc.
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
            // A Wheel is touched only at its hub, as it is drawn over its links.
            var radius = Math.Max(tolerance, ToGodotFloat(Definition!.CoveredRadius(Definition.Nodes[nodeIndex].Id), nameof(JointMotor.Radius)));
            if (_nodeVisuals[nodeIndex].GlobalPosition.DistanceSquaredTo(globalPosition) <= radius * radius)
            {
                var nodeId = Definition!.Nodes[nodeIndex].Id;
                selection = Definition.HingeParts.FirstOrDefault(part => part.NodeId == nodeId) is { } hingePart
                    ? new CreatureElementSelection(hingePart.Kind, hingePart.Id)
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
            if (_beamGone[beamIndex])
            {
                continue;
            }

            var halfLength = _beamHalfLengths[beamIndex];
            var start = body.ToGlobal(new Vector2(-halfLength, 0));
            var end = body.ToGlobal(new Vector2(halfLength, 0));
            if (DistanceSquaredToSegment(globalPosition, start, end) <= tolerance * tolerance)
            {
                selection = new CreatureElementSelection(CreatureElementKind.Beam, Definition!.Beams[beamIndex].Id);
                return true;
            }
        }

        // Off every part and reach, a Wheel's face or tyre stands for the Wheel.
        if (DrawGroups.WheelDiscAt(ToDomain(globalPosition), nodeId => ToDomain(_nodeVisuals[Definition!.NodeIndexOf(nodeId)].GlobalPosition), Definition!.Wheels) is { } disc)
        {
            selection = new CreatureElementSelection(CreatureElementKind.Wheel, disc.Id);
            return true;
        }

        selection = null;
        return false;
    }

    private static Vector2D ToDomain(Vector2 position) => new(position.X, position.Y);

    // The part drawn on top at the point, by the rule Build's touches follow too.
    private CreatureElementSelection? DrawnPartAt(Vector2 globalPosition)
    {
        if (Definition is not { } definition || _drawGroups is not { } groups)
        {
            return null;
        }

        return groups.PartAt(
            ToDomain(globalPosition),
            nodeId => ToDomain(_nodeVisuals[definition.NodeIndexOf(nodeId)].GlobalPosition),
            definition.HingeParts,
            definition.Wheels,
            definition.Sensors,
            _raised);
    }

    /// <summary>
    /// Where a part's name points to, in global coordinates: a joint's centre, a beam's or
    /// link's middle, or a sensor's picture.
    /// </summary>
    public Vector2 PartAnchor(CreatureElementSelection selection)
    {
        if (_parts.TryOf(selection, out var entry))
        {
            return entry.Value.Anchor();
        }

        GD.PushError($"No {selection.Kind} {selection.Id} to name; its name points to the creature.");
        return GlobalPosition;
    }

    // A part's visual, which draws it and its selection, none for a Touch sensor or Rubber,
    // which their joint draws, and where its name points.
    private readonly record struct PartView(PartVisual? Visual, Func<Vector2> Anchor);

    // Every part by its id. The typed arrays line up with the definition's lists.
    private void IndexParts(CreatureDef definition)
    {
        _parts = new PartIndex<PartView>();
        IndexAtVisual(definition.Nodes, _nodeVisuals);
        IndexAtVisual(definition.Sensors, _sensorVisuals);
        IndexAtVisual(definition.Servos, _servoVisuals);
        IndexAtVisual(definition.VelocityMotors, _velocityMotorVisuals);
        IndexAtVisual(definition.Brakes, _brakeVisuals);
        IndexAtVisual(definition.AngleSensors, _angleSensorVisuals);
        IndexAtVisual(definition.Steppers, _stepperVisuals);
        IndexAtVisual(definition.Wheels, _wheelVisuals);
        for (var i = 0; i < definition.Beams.Count; i++)
        {
            var body = _beamBodies[i];
            _parts.Add(definition.Beams[i], new PartView(_beamVisuals[i], () => body.GlobalPosition));
        }

        for (var i = 0; i < definition.Pistons.Count; i++)
        {
            var piston = _pistons[i];
            _parts.Add(definition.Pistons[i], new PartView(_pistonVisuals[i], () => Middle(piston.NodeA, piston.NodeB)));
        }

        for (var i = 0; i < definition.Springs.Count; i++)
        {
            var spring = _springVisuals[i];
            _parts.Add(definition.Springs[i], new PartView(spring, () => Middle(spring.NodeA, spring.NodeB)));
        }

        foreach (var jointPart in definition.TouchSensors.Concat<IJointPartDef>(definition.Rubbers))
        {
            var joint = _nodeVisuals[definition.NodeIndexOf(jointPart.NodeId)];
            _parts.Add(jointPart, new PartView(null, () => joint.GlobalPosition));
        }

        // PartIndexTests keeps every part list here; a part still left out is logged, shown unselectable
        // and named at the creature, never thrown mid-build.
        if (_parts.Missing(definition.Parts) is { Count: > 0 } missing)
        {
            GD.PushError($"Not indexed: {string.Join(", ", missing)}.");
        }

        static Vector2 Middle(Node2D a, Node2D b) => (a.GlobalPosition + b.GlobalPosition) / 2;
    }

    private void IndexAtVisual<TPart>(IReadOnlyList<TPart> parts, IReadOnlyList<PartVisual> visuals)
        where TPart : IPartDef
    {
        for (var i = 0; i < parts.Count; i++)
        {
            var visual = visuals[i];
            _parts.Add(parts[i], new PartView(visual, () => visual.GlobalPosition));
        }
    }

    /// <summary>Marks the <paramref name="parts"/> selected, a stacked joint's all at once; none with an empty list.</summary>
    public void SetSelectedParts(IReadOnlyList<CreatureElementSelection> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        _selection = parts;
        ApplySelection();
    }

    // A shadow never shows a selection, so its parts stay on their unselected layers.
    private void ApplySelection()
    {
        foreach (var (_, view) in _parts.Entries)
        {
            view.Visual?.Selected = false;
        }

        foreach (var visual in _nodeVisuals)
        {
            visual.TouchSelected = false;
            visual.WheelSelected = false;
            visual.SkinSelected = false;
        }

        var selection = _isShadow ? [] : _selection;
        ApplyDrawGroups(selection);
        foreach (var selected in selection)
        {
            if (!_parts.TryOf(selected, out var entry))
            {
                GD.PushError($"No {selected.Kind} {selected.Id} to mark selected.");
                continue;
            }

            var (part, view) = entry;
            switch (part)
            {
                // A Touch sensor's mark is its joint's ring outside the cushion; the joint's own stays unselected.
                case TouchSensorDef touch:
                    JointVisualOf(touch).TouchSelected = true;
                    break;

                // A Rubber's mark is its skin in halo, drawn by its joint.
                case RubberDef rubber:
                    JointVisualOf(rubber).SkinSelected = true;
                    break;

                // A Wheel's joint draws its mark with the hub, over the links.
                case WheelDef wheel:
                    JointVisualOf(wheel).WheelSelected = true;
                    break;

                default:
                    view.Visual!.Selected = true;
                    break;
            }
        }
    }

    private NodeVisual JointVisualOf(IJointPartDef part) => _nodeVisuals[Definition!.NodeIndexOf(part.NodeId)];

    // Each part in its draw group, with what belongs with the selection raised as in Build's CreatureParts.
    private void ApplyDrawGroups(IReadOnlyList<CreatureElementSelection> selection)
    {
        if (Definition is not { } definition || _drawGroups is not { } groups)
        {
            return;
        }

        _raised = groups.Raised(selection, definition.HingeParts, definition.Wheels, definition.TouchSensors, definition.Rubbers);
        foreach (var (part, view) in _parts.Entries)
        {
            view.Visual?.Place(groups, part, _raised);
        }
    }
}
