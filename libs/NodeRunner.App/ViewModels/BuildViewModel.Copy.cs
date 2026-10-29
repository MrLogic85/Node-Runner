using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The selection panel's Copy: several selected parts duplicated beside themselves,
/// inside the same Creation. A part copies only with what it sits on: a link with both its joints
/// and a sensor with its beam. A joint part, a Servo, a Velocity motor, a Brake, an Angle sensor, a Wheel, a Touch sensor or a Rubber, is its joint
/// to the player, so it brings that joint along. A locked Creation copies no part with
/// network ports, since a copy would change its network; a Wheel, a Rubber and a Brake have none.
/// </summary>
public sealed partial class BuildViewModel
{
    /// <summary>
    /// How far a copy goes aside, each way: the fewest whole <see cref="GridStep"/>s that make at
    /// least <see cref="BuildAreaCell"/>, so a copy never covers its original and lands on the grid.
    /// </summary>
    public double CopyStep => Math.Ceiling(BuildAreaCell / GridStep) * GridStep;

    /// <summary>Whether enough parts are selected to copy: two or more, locked or not.</summary>
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
            AddNetworkPortNotes(CreatureElementKind.Sensor);
            AddNetworkPortNotes(CreatureElementKind.Servo);
            AddNetworkPortNotes(CreatureElementKind.VelocityMotor);
            AddNetworkPortNotes(CreatureElementKind.Piston);
            AddNetworkPortNotes(CreatureElementKind.TouchSensor);
            AddNetworkPortNotes(CreatureElementKind.AngleSensor);
            AddNetworkPortNotes(CreatureElementKind.Stepper);
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

        foreach (var sensor in Sensors.Where(sensor => IsSelected(CreatureElementKind.Sensor, sensor.Id) && !IsSelected(CreatureElementKind.Beam, sensor.BeamId)))
        {
            notes.Add(Note(CreatureElementKind.Sensor, sensor.Id, UiText.Plain("Select its beam")));
        }

        return notes;

        void AddNetworkPortNotes(CreatureElementKind kind)
        {
            foreach (var id in SelectedIds(kind).Order())
            {
                notes.Add(Note(kind, id, _wouldChangeNetworkNote));
            }
        }

        void AddOpenLinkNote(CreatureElementKind kind, int id, int nodeA, int nodeB)
        {
            if (IsSelected(kind, id) && !(joints.Contains(nodeA) && joints.Contains(nodeB)))
            {
                notes.Add(Note(kind, id, UiText.Plain("Select both its joints")));
            }
        }

        static CanvasNote Note(CreatureElementKind kind, int id, UiText text) =>
            new(CanvasNoteKind.Danger, new CreatureElementSelection(kind, id), text);
    }

    /// <summary>
    /// Duplicates the selection <see cref="CopyStep"/> aside and selects the copy, as one undo step that
    /// selects the originals again. Each copy keeps its settings but not its name; a copied motor, Brake or Angle sensor
    /// sits on the copy of its joint, uses the copies of its Fixed and Target links or Wheel, and
    /// leaves a role empty whose side was not copied. A copied joint part brings its joint along, but the
    /// joint's other parts only when they are selected too. The copy is selected as the
    /// original was: joint parts stand in for their copied joint. While Copy is dimmed, it instead shows <see cref="CopyBlockers"/> in
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
        var selectedJoints = SelectedIds(CreatureElementKind.Node).ToHashSet();
        var nodes = Nodes.Where(node => joints.Contains(node.Id)).ToList();
        var beams = Beams.Where(beam => IsSelected(CreatureElementKind.Beam, beam.Id)).ToList();
        var pistons = Pistons.Where(piston => IsSelected(CreatureElementKind.Piston, piston.Id)).ToList();
        var springs = Springs.Where(spring => IsSelected(CreatureElementKind.Spring, spring.Id)).ToList();
        var sensors = Sensors.Where(sensor => IsSelected(CreatureElementKind.Sensor, sensor.Id)).ToList();
        var servos = Servos.Where(servo => IsSelected(CreatureElementKind.Servo, servo.Id)).ToList();
        var velocityMotors = VelocityMotors.Where(motor => IsSelected(CreatureElementKind.VelocityMotor, motor.Id)).ToList();
        var steppers = Steppers.Where(stepper => IsSelected(CreatureElementKind.Stepper, stepper.Id)).ToList();
        var brakes = Brakes.Where(brake => IsSelected(CreatureElementKind.Brake, brake.Id)).ToList();
        var angleSensors = AngleSensors.Where(angleSensor => IsSelected(CreatureElementKind.AngleSensor, angleSensor.Id)).ToList();
        var wheels = Wheels.Where(wheel => IsSelected(CreatureElementKind.Wheel, wheel.Id)).ToList();
        var touchSensors = TouchSensors.Where(touch => IsSelected(CreatureElementKind.TouchSensor, touch.Id)).ToList();
        var rubbers = Rubbers.Where(rubber => IsSelected(CreatureElementKind.Rubber, rubber.Id)).ToList();

        // A copied joint is as big as the joint parts that come along with it.
        var copiedRadii = nodes.ToDictionary(node => node.Id, node => CreatureDef.JointRadius(node.Id, [.. servos, .. velocityMotors, .. steppers], wheels));
        var offset = CopyOffset(nodes, copiedRadii);
        _history.Change(() =>
        {
            _selected.Clear();
            var nodeCopies = new Dictionary<int, int>();
            var linkCopies = new Dictionary<int, int>();
            foreach (var node in nodes)
            {
                var position = new Vector2D(node.Position.X + offset.X, node.Position.Y + offset.Y);
                // Clamping too absorbs the rounding in a shortened offset, as in TranslateSelection.
                nodeCopies[node.Id] = _builder.AddNode(BuildArea.Clamp(position, copiedRadii[node.Id]));
                if (selectedJoints.Contains(node.Id))
                {
                    _selected.Add(new CreatureElementSelection(CreatureElementKind.Node, nodeCopies[node.Id]));
                }
            }

            foreach (var beam in beams)
            {
                linkCopies[beam.Id] = _builder.AddBeam(nodeCopies[beam.NodeA], nodeCopies[beam.NodeB]);
                _selected.Add(new CreatureElementSelection(CreatureElementKind.Beam, linkCopies[beam.Id]));
            }

            foreach (var piston in pistons)
            {
                linkCopies[piston.Id] = CopySettings(piston.Id, _builder.AddPiston(nodeCopies[piston.NodeA], nodeCopies[piston.NodeB]));
                _selected.Add(new CreatureElementSelection(CreatureElementKind.Piston, linkCopies[piston.Id]));
            }

            foreach (var spring in springs)
            {
                linkCopies[spring.Id] = CopySettings(spring.Id, _builder.AddSpring(nodeCopies[spring.NodeA], nodeCopies[spring.NodeB]));
                _selected.Add(new CreatureElementSelection(CreatureElementKind.Spring, linkCopies[spring.Id]));
            }

            foreach (var sensor in sensors)
            {
                _builder.AddSensor(linkCopies[sensor.BeamId], sensor.Kind, out var copy, out _);
                _selected.Add(new CreatureElementSelection(CreatureElementKind.Sensor, CopySettings(sensor.Id, copy)));
            }

            // A Wheel before its motor: to the motor it is a one-sided link.
            foreach (var wheel in wheels)
            {
                linkCopies[wheel.Id] = CopySettings(wheel.Id, _builder.AddWheel(nodeCopies[wheel.NodeId]));
                _selected.Add(new CreatureElementSelection(CreatureElementKind.Wheel, linkCopies[wheel.Id]));
            }

            foreach (var servo in servos)
            {
                var copy = _builder.AddServo(nodeCopies[servo.NodeId], CopyOf(servo.FixedLinkId), CopyOf(servo.TargetLinkId));
                _selected.Add(new CreatureElementSelection(CreatureElementKind.Servo, CopySettings(servo.Id, copy)));
            }

            foreach (var motor in velocityMotors)
            {
                var copy = _builder.AddVelocityMotor(nodeCopies[motor.NodeId], CopyOf(motor.FixedLinkId), CopyOf(motor.TargetLinkId));
                _selected.Add(new CreatureElementSelection(CreatureElementKind.VelocityMotor, CopySettings(motor.Id, copy)));
            }

            foreach (var stepper in steppers)
            {
                var copy = _builder.AddStepper(nodeCopies[stepper.NodeId], CopyOf(stepper.FixedLinkId), CopyOf(stepper.TargetLinkId));
                _selected.Add(new CreatureElementSelection(CreatureElementKind.Stepper, CopySettings(stepper.Id, copy)));
            }

            foreach (var brake in brakes)
            {
                var copy = _builder.AddBrake(nodeCopies[brake.NodeId], CopyOf(brake.FixedLinkId), CopyOf(brake.TargetLinkId));
                _selected.Add(new CreatureElementSelection(CreatureElementKind.Brake, CopySettings(brake.Id, copy)));
            }

            foreach (var angleSensor in angleSensors)
            {
                _selected.Add(new CreatureElementSelection(CreatureElementKind.AngleSensor, _builder.AddAngleSensor(nodeCopies[angleSensor.NodeId], CopyOf(angleSensor.FixedLinkId), CopyOf(angleSensor.TargetLinkId))));
            }

            foreach (var touch in touchSensors)
            {
                _selected.Add(new CreatureElementSelection(CreatureElementKind.TouchSensor, _builder.AddTouchSensor(nodeCopies[touch.NodeId])));
            }

            foreach (var rubber in rubbers)
            {
                _selected.Add(new CreatureElementSelection(CreatureElementKind.Rubber, _builder.AddRubber(nodeCopies[rubber.NodeId])));
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

    // CopyStep down and right, or the first other diagonal that keeps every copied joint inside
    // the build area; if none does, the first diagonal shortened so the copy keeps its shape.
    private Vector2D CopyOffset(IReadOnlyList<NodeDef> nodes, Dictionary<int, double> radii)
    {
        var joints = nodes.Select(node => (node.Position, radii[node.Id])).ToList();
        var step = CopyStep;
        Vector2D[] offsets =
        [
            new(step, step),
            new(-step, step),
            new(step, -step),
            new(-step, -step),
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
