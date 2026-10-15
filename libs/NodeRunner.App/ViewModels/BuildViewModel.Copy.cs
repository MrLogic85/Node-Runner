using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The selection panel's Copy (#937, #990): several selected parts duplicated beside themselves,
/// inside the same Creation. A part copies only with what it sits on: a link with both its joints
/// and a sensor with its beam. A joint part, a Servo, a Velocity motor, a Brake (#1041), an Angle sensor (#1206), a Wheel, a Touch sensor (#1045) or a Rubber (#1142), is its joint
/// to the player (#973), so it brings that joint along (#1000). A locked Creation copies no part with
/// network ports, since a copy would change its network; a Wheel, a Rubber and a Brake have none.
/// </summary>
public sealed partial class BuildViewModel
{
    /// <summary>Whether enough parts are selected to copy: two or more, locked or not (#990).</summary>
    public bool CanOfferCopy => SelectedPartCount >= 2;

    /// <summary>Whether Copy would duplicate the selection now; when not, the button is dimmed.</summary>
    public bool CanCopySelection => CanOfferCopy && CopyBlockers().Count == 0;

    /// <summary>
    /// A danger note at each selected part a copy cannot take, in id order within each kind: on a
    /// locked Creation a sensor, motor, Piston, Touch sensor or Angle sensor for its network ports; a beam, Spring or Piston
    /// without both joints among <see cref="SelectedNodeIds"/>; a sensor without its beam.
    /// </summary>
    public IReadOnlyList<CanvasNote> CopyBlockers()
    {
        var notes = new List<CanvasNote>();
        var joints = SelectedNodeIds;
        if (_locked)
        {
            AddNetworkPortNotes(CreatureElementKind.Sensor, _selectedSensorIds);
            AddNetworkPortNotes(CreatureElementKind.Servo, _selectedServoIds);
            AddNetworkPortNotes(CreatureElementKind.VelocityMotor, _selectedVelocityMotorIds);
            AddNetworkPortNotes(CreatureElementKind.Piston, _selectedPistonIds);
            AddNetworkPortNotes(CreatureElementKind.TouchSensor, _selectedTouchSensorIds);
            AddNetworkPortNotes(CreatureElementKind.AngleSensor, _selectedAngleSensorIds);
            AddNetworkPortNotes(CreatureElementKind.Stepper, _selectedStepperIds);
        }

        foreach (var beam in Beams)
        {
            AddOpenLinkNote(CreatureElementKind.Beam, beam.Id, beam.NodeA, beam.NodeB);
        }

        foreach (var spring in Springs)
        {
            AddOpenLinkNote(CreatureElementKind.Spring, spring.Id, spring.NodeA, spring.NodeB);
        }

        // A part with network ports already has its note on a locked Creation.
        if (_locked)
        {
            return notes;
        }

        foreach (var piston in Pistons)
        {
            AddOpenLinkNote(CreatureElementKind.Piston, piston.Id, piston.NodeA, piston.NodeB);
        }

        foreach (var sensor in Sensors.Where(sensor => _selectedSensorIds.Contains(sensor.Id) && !_selectedBeamIds.Contains(sensor.BeamId)))
        {
            notes.Add(Note(CreatureElementKind.Sensor, sensor.Id, UiText.Plain("Select its beam")));
        }

        return notes;

        void AddNetworkPortNotes(CreatureElementKind kind, HashSet<int> selected)
        {
            foreach (var id in selected.Order())
            {
                notes.Add(Note(kind, id, _wouldChangeNetworkNote));
            }
        }

        void AddOpenLinkNote(CreatureElementKind kind, int id, int nodeA, int nodeB)
        {
            if (SelectedSet(kind).Contains(id) && !(joints.Contains(nodeA) && joints.Contains(nodeB)))
            {
                notes.Add(Note(kind, id, UiText.Plain("Select both its joints")));
            }
        }

        static CanvasNote Note(CreatureElementKind kind, int id, UiText text) =>
            new(CanvasNoteKind.Danger, new CreatureElementSelection(kind, id), text);
    }

    /// <summary>
    /// Duplicates the selection one grid step aside and selects the copy, as one undo step that
    /// selects the originals again. Each copy keeps its settings but not its name; a copied motor, Brake or Angle sensor
    /// sits on the copy of its joint, uses the copies of its Fixed and Target links or Wheel, and
    /// leaves a role empty whose side was not copied. A copied joint part brings its joint along, but the
    /// joint's other parts only when they are selected too (#1044). The copy is selected as the
    /// original was: joint parts stand in for their copied joint (#973). While Copy is dimmed, it instead shows <see cref="CopyBlockers"/> in
    /// <see cref="CanvasNotes"/>.
    /// </summary>
    public void CopySelectedParts()
    {
        if (!CanOfferCopy)
        {
            return;
        }

        var blockers = CopyBlockers();
        if (blockers.Count > 0)
        {
            ShowRefusalNotes(blockers);
            return;
        }

        var joints = SelectedNodeIds;
        var selectedJoints = _selectedNodeIds.ToHashSet();
        var nodes = Nodes.Where(node => joints.Contains(node.Id)).ToList();
        var beams = Beams.Where(beam => _selectedBeamIds.Contains(beam.Id)).ToList();
        var pistons = Pistons.Where(piston => _selectedPistonIds.Contains(piston.Id)).ToList();
        var springs = Springs.Where(spring => _selectedSpringIds.Contains(spring.Id)).ToList();
        var sensors = Sensors.Where(sensor => _selectedSensorIds.Contains(sensor.Id)).ToList();
        var servos = Servos.Where(servo => _selectedServoIds.Contains(servo.Id)).ToList();
        var velocityMotors = VelocityMotors.Where(motor => _selectedVelocityMotorIds.Contains(motor.Id)).ToList();
        var steppers = Steppers.Where(stepper => _selectedStepperIds.Contains(stepper.Id)).ToList();
        var brakes = Brakes.Where(brake => _selectedBrakeIds.Contains(brake.Id)).ToList();
        var angleSensors = AngleSensors.Where(angleSensor => _selectedAngleSensorIds.Contains(angleSensor.Id)).ToList();
        var wheels = Wheels.Where(wheel => _selectedWheelIds.Contains(wheel.Id)).ToList();
        var touchSensors = TouchSensors.Where(touch => _selectedTouchSensorIds.Contains(touch.Id)).ToList();
        var rubbers = Rubbers.Where(rubber => _selectedRubberIds.Contains(rubber.Id)).ToList();

        // A copied joint is as big as the joint parts that come along with it (#1044).
        var copiedRadii = nodes.ToDictionary(node => node.Id, node => CreatureDef.JointRadius(node.Id, [.. servos, .. velocityMotors, .. steppers], wheels));
        var offset = CopyOffset(nodes, copiedRadii);
        _history.Change(() =>
        {
            ClearSelectionSets();
            var nodeCopies = new Dictionary<int, int>();
            var linkCopies = new Dictionary<int, int>();
            foreach (var node in nodes)
            {
                var position = new Vector2D(node.Position.X + offset.X, node.Position.Y + offset.Y);
                // Clamping too absorbs the rounding in a shortened offset, as in TranslateSelection.
                nodeCopies[node.Id] = _builder.AddNode(BuildArea.Clamp(position, copiedRadii[node.Id]));
                if (selectedJoints.Contains(node.Id))
                {
                    _selectedNodeIds.Add(nodeCopies[node.Id]);
                }
            }

            foreach (var beam in beams)
            {
                linkCopies[beam.Id] = _builder.AddBeam(nodeCopies[beam.NodeA], nodeCopies[beam.NodeB]);
                _selectedBeamIds.Add(linkCopies[beam.Id]);
            }

            foreach (var piston in pistons)
            {
                linkCopies[piston.Id] = CopySettings(piston.Id, _builder.AddPiston(nodeCopies[piston.NodeA], nodeCopies[piston.NodeB]));
                _selectedPistonIds.Add(linkCopies[piston.Id]);
            }

            foreach (var spring in springs)
            {
                linkCopies[spring.Id] = CopySettings(spring.Id, _builder.AddSpring(nodeCopies[spring.NodeA], nodeCopies[spring.NodeB]));
                _selectedSpringIds.Add(linkCopies[spring.Id]);
            }

            foreach (var sensor in sensors)
            {
                _builder.AddSensor(linkCopies[sensor.BeamId], sensor.Kind, out var copy, out _);
                _selectedSensorIds.Add(CopySettings(sensor.Id, copy));
            }

            // A Wheel before its motor: to the motor it is a one-sided link (#1118).
            foreach (var wheel in wheels)
            {
                linkCopies[wheel.Id] = CopySettings(wheel.Id, _builder.AddWheel(nodeCopies[wheel.NodeId]));
                _selectedWheelIds.Add(linkCopies[wheel.Id]);
            }

            foreach (var servo in servos)
            {
                var copy = _builder.AddServo(nodeCopies[servo.NodeId], CopyOf(servo.FixedLinkId), CopyOf(servo.TargetLinkId));
                _selectedServoIds.Add(CopySettings(servo.Id, copy));
            }

            foreach (var motor in velocityMotors)
            {
                var copy = _builder.AddVelocityMotor(nodeCopies[motor.NodeId], CopyOf(motor.FixedLinkId), CopyOf(motor.TargetLinkId));
                _selectedVelocityMotorIds.Add(CopySettings(motor.Id, copy));
            }

            foreach (var stepper in steppers)
            {
                var copy = _builder.AddStepper(nodeCopies[stepper.NodeId], CopyOf(stepper.FixedLinkId), CopyOf(stepper.TargetLinkId));
                _selectedStepperIds.Add(CopySettings(stepper.Id, copy));
            }

            foreach (var brake in brakes)
            {
                var copy = _builder.AddBrake(nodeCopies[brake.NodeId], CopyOf(brake.FixedLinkId), CopyOf(brake.TargetLinkId));
                _selectedBrakeIds.Add(CopySettings(brake.Id, copy));
            }

            foreach (var angleSensor in angleSensors)
            {
                _selectedAngleSensorIds.Add(_builder.AddAngleSensor(nodeCopies[angleSensor.NodeId], CopyOf(angleSensor.FixedLinkId), CopyOf(angleSensor.TargetLinkId)));
            }

            foreach (var touch in touchSensors)
            {
                _selectedTouchSensorIds.Add(_builder.AddTouchSensor(nodeCopies[touch.NodeId]));
            }

            foreach (var rubber in rubbers)
            {
                _selectedRubberIds.Add(_builder.AddRubber(nodeCopies[rubber.NodeId]));
            }

            int? CopyOf(int? linkId) => linkId is { } id && linkCopies.TryGetValue(id, out var copy) ? copy : null;
        }, Selection);
        NotifySelectionChanged();
        RaiseAnatomyChanged();
    }

    private int CopySettings(int fromPartId, int toPartId)
    {
        foreach (var parameter in _builder.ParametersOf(fromPartId))
        {
            _builder.SetParameter(toPartId, parameter, _builder.ParameterValue(fromPartId, parameter));
        }

        return toPartId;
    }

    // One grid step down and right, or the first other diagonal that keeps every copied joint inside
    // the build area; if none does, the first diagonal shortened so the copy keeps its shape.
    private static Vector2D CopyOffset(IReadOnlyList<NodeDef> nodes, Dictionary<int, double> radii)
    {
        var joints = nodes.Select(node => (node.Position, radii[node.Id])).ToList();
        Vector2D[] offsets =
        [
            new(BuildGridStep, BuildGridStep),
            new(-BuildGridStep, BuildGridStep),
            new(BuildGridStep, -BuildGridStep),
            new(-BuildGridStep, -BuildGridStep),
        ];
        foreach (var offset in offsets)
        {
            if (ShortenedToBuildArea(joints, offset) == offset)
            {
                return offset;
            }
        }

        return ShortenedToBuildArea(joints, offsets[0]);
    }
}
