using System.Diagnostics.CodeAnalysis;
using NodeRunner.App.Builders;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// Placing parts from the tray and moving a sensor or Memory cell to another beam: one rule
/// for where a sensor or Memory cell goes, and a good drop is one Undo step that selects the part.
/// </summary>
public sealed partial class BuildViewModel
{
    /// <summary>
    /// Whether a tray part dropped on <paramref name="target"/> would be placed there; if
    /// not, <paramref name="reason"/> says why. Sensors and Memory cells go on a beam with neither yet, and joint
    /// parts on a joint whose slot for them is free: one Wheel, one hinge part, one Touch sensor and one Rubber.
    /// </summary>
    public bool CanPlacePart(BuildPart part, CreatureElementSelection target, [NotNullWhen(false)] out UiText? reason)
    {
        ArgumentNullException.ThrowIfNull(target);
        // Every tray part but the Wheel, the Rubber and the Brake has network ports.
        if (_locked && PartTray.HasNetworkPorts(part))
        {
            reason = LockedReason;
            return false;
        }

        if (!PartTray.IsAvailable(part) || !PartTray.IsPlaceable(part))
        {
            reason = PartTray.ComingLater;
            return false;
        }

        if (part == BuildPart.Servo)
        {
            if (target.Kind != CreatureElementKind.Node)
            {
                reason = CreatureBuilder.ServosGoOnAJointReason;
                return false;
            }

            return _builder.CanAddServo(target.Id, out reason);
        }

        if (part == BuildPart.Stepper)
        {
            if (target.Kind != CreatureElementKind.Node)
            {
                reason = CreatureBuilder.SteppersGoOnAJointReason;
                return false;
            }

            return _builder.CanAddStepper(target.Id, out reason);
        }

        if (part == BuildPart.VelocityMotor)
        {
            if (target.Kind != CreatureElementKind.Node)
            {
                reason = CreatureBuilder.VelocityMotorsGoOnAJointReason;
                return false;
            }

            return _builder.CanAddVelocityMotor(target.Id, out reason);
        }

        if (part == BuildPart.Brake)
        {
            if (target.Kind != CreatureElementKind.Node)
            {
                reason = CreatureBuilder.BrakesGoOnAJointReason;
                return false;
            }

            return _builder.CanAddBrake(target.Id, out reason);
        }

        if (part == BuildPart.AngleSensor)
        {
            if (target.Kind != CreatureElementKind.Node)
            {
                reason = CreatureBuilder.AngleSensorsGoOnAJointReason;
                return false;
            }

            return _builder.CanAddAngleSensor(target.Id, out reason);
        }

        if (part == BuildPart.Wheel)
        {
            if (target.Kind != CreatureElementKind.Node)
            {
                reason = CreatureBuilder.WheelsGoOnAJointReason;
                return false;
            }

            return _builder.CanAddWheel(target.Id, out reason);
        }

        if (part == BuildPart.TouchSensor)
        {
            if (target.Kind != CreatureElementKind.Node)
            {
                reason = CreatureBuilder.TouchSensorsGoOnAJointReason;
                return false;
            }

            return _builder.CanAddTouchSensor(target.Id, out reason);
        }

        if (part == BuildPart.Rubber)
        {
            if (target.Kind != CreatureElementKind.Node)
            {
                reason = CreatureBuilder.RubberGoesOnAJointReason;
                return false;
            }

            return _builder.CanAddRubber(target.Id, out reason);
        }

        if (part == BuildPart.MemoryCell)
        {
            return TakesBeamPart(CreatureBuilder.MemoryCellsGoOnABeamReason, target, movingBeamPartId: null, out reason);
        }

        return TakesBeamPart(GoesOnABeamReason(PartTray.SensorKindOf(part)!.Value), target, movingBeamPartId: null, out reason);
    }

    /// <summary>
    /// Places a tray part dropped or tapped on <paramref name="target"/> and returns its new id.
    /// A drop on empty canvas (<paramref name="target"/> null) changes nothing and says
    /// nothing; a refused drop changes nothing and shows why as <see cref="PlacementNote"/> at that
    /// part. A dropped part is selected; a tapped one is not, so the tray stays for the next.
    /// </summary>
    public int? PlacePart(BuildPart part, CreatureElementSelection? target, bool select = true)
    {
        PlacementNote = null;
        if (target is null)
        {
            return null;
        }

        if (!CanPlacePart(part, target, out var reason))
        {
            // A locked Creation says why in a notification instead.
            if (!(_locked && PartTray.HasNetworkPorts(part)))
            {
                PlacementNote = new CanvasNote(CanvasNoteKind.Danger, target, reason);
            }

            return null;
        }

        if (part == BuildPart.Servo)
        {
            var servoId = _history.Change(() => _builder.AddServo(target.Id));
            SelectPlaced(PartSet.Of(new(CreatureElementKind.Servo, servoId)), select);
            return servoId;
        }

        if (part == BuildPart.Stepper)
        {
            var stepperId = _history.Change(() => _builder.AddStepper(target.Id));
            SelectPlaced(PartSet.Of(new(CreatureElementKind.Stepper, stepperId)), select);
            return stepperId;
        }

        if (part == BuildPart.VelocityMotor)
        {
            var motorId = _history.Change(() => _builder.AddVelocityMotor(target.Id));
            SelectPlaced(PartSet.Of(new(CreatureElementKind.VelocityMotor, motorId)), select);
            return motorId;
        }

        if (part == BuildPart.Brake)
        {
            var brakeId = _history.Change(() => _builder.AddBrake(target.Id));
            SelectPlaced(PartSet.Of(new(CreatureElementKind.Brake, brakeId)), select);
            return brakeId;
        }

        if (part == BuildPart.AngleSensor)
        {
            var angleSensorId = _history.Change(() => _builder.AddAngleSensor(target.Id));
            SelectPlaced(PartSet.Of(new(CreatureElementKind.AngleSensor, angleSensorId)), select);
            return angleSensorId;
        }

        if (part == BuildPart.Wheel)
        {
            var wheelId = _history.Change(() => _builder.AddWheel(target.Id));
            SelectPlaced(PartSet.Of(new(CreatureElementKind.Wheel, wheelId)), select);
            return wheelId;
        }

        if (part == BuildPart.TouchSensor)
        {
            var touchId = _history.Change(() => _builder.AddTouchSensor(target.Id));
            SelectPlaced(PartSet.Of(new(CreatureElementKind.TouchSensor, touchId)), select);
            return touchId;
        }

        if (part == BuildPart.Rubber)
        {
            var rubberId = _history.Change(() => _builder.AddRubber(target.Id));
            SelectPlaced(PartSet.Of(new(CreatureElementKind.Rubber, rubberId)), select);
            return rubberId;
        }

        if (part == BuildPart.MemoryCell)
        {
            var memoryCellId = _history.Change(() => _builder.AddMemoryCell(target.Id));
            SelectPlaced(PartSet.Of(new(CreatureElementKind.MemoryCell, memoryCellId)), select);
            return memoryCellId;
        }

        var sensorId = _history.Change(() =>
        {
            _builder.AddSensor(target.Id, PartTray.SensorKindOf(part)!.Value, out var id, out _);
            return id;
        });
        SelectPlaced(PartSet.Of(new(CreatureElementKind.Sensor, sensorId)), select);
        return sensorId;
    }

    private void SelectPlaced(PartSet placed, bool select)
    {
        if (select)
        {
            ReplaceSelection(placed);
        }
        else
        {
            RaiseAnatomyChanged();
        }
    }

    /// <summary>
    /// Whether the sensor or Memory cell dragged onto <paramref name="target"/> would move there: onto a
    /// beam no other sensor or Memory cell sits on, its own included. If not, <paramref name="reason"/> says why.
    /// A move keeps the part's network ports, so a locked Creation moves them too. The part
    /// must exist.
    /// </summary>
    public bool CanMoveBeamPart(int partId, [NotNullWhen(true)] CreatureElementSelection? target, [NotNullWhen(false)] out UiText? reason) =>
        TakesBeamPart(GoesOnABeamReason(ExistingBeamPart(partId)), target, partId, out reason);

    /// <summary>
    /// The radius a ring that marks joint <paramref name="nodeId"/> is drawn round, at its
    /// <see cref="SelectionMarks.JointHalo"/>: <paramref name="radius"/>, or an Angle sensor's halo there
    /// when that lies further out, so a link's or a placed part's ring never covers the sensor's arc.
    /// The joint part being placed there (<paramref name="placing"/>) counts as on the joint: an Angle sensor
    /// brings the arc, and a Rubber or Touch sensor moves it out.
    /// </summary>
    public double RingRadiusAt(int nodeId, double radius, BuildPart? placing = null)
    {
        if (placing != BuildPart.AngleSensor && _builder.AngleSensors.All(sensor => sensor.NodeId != nodeId))
        {
            return radius;
        }

        var arc = AngleSensorDef.ArcRadiusAt(
            nodeId,
            _builder.Rubbers.Select(rubber => rubber.NodeId).Concat(placing == BuildPart.Rubber ? [nodeId] : []),
            _builder.TouchSensors.Select(touch => touch.NodeId).Concat(placing == BuildPart.TouchSensor ? [nodeId] : []));
        return Math.Max(radius, AngleSensorDef.MarkRadius(arc));
    }

    /// <summary>
    /// The beams or joints that would take or refuse <paramref name="part"/> from the tray, or else
    /// the beams that would take or refuse the sensor or Memory cell <paramref name="movingBeamPartId"/> a drag moves.
    /// </summary>
    public PlacingTargets PlacingTargetsOf(BuildPart? part, int? movingBeamPartId)
    {
        if (part is { } placing)
        {
            if (PartTray.IsJointPart(placing))
            {
                var joints = Nodes.Where(node => CanPlacePart(placing, new CreatureElementSelection(CreatureElementKind.Node, node.Id), out _)).Select(node => node.Id).ToHashSet();
                var refused = Nodes.Where(node => !joints.Contains(node.Id) && JointPartsAt(node.Id).Count > 0).Select(node => node.Id).ToHashSet();
                return PlacingTargets.None with
                {
                    Joints = joints,
                    RefusedJoints = refused,
                    JointRings = Nodes.ToDictionary(
                        node => node.Id,
                        node => RingRadiusAt(node.Id, PartTray.PlacingRingRadius(placing, CreatureDef.JointRadius(node.Id, JointMotors, Wheels)), placing)),
                    RefusedJointRings = refused.ToDictionary(id => id, id => RingRadiusAt(id, NodeRadius(id))),
                };
            }

            var beams = Beams.Where(beam => CanPlacePart(placing, new CreatureElementSelection(CreatureElementKind.Beam, beam.Id), out _)).Select(beam => beam.Id).ToHashSet();
            return PlacingTargets.None with { Beams = beams, RefusedBeams = Beams.Where(beam => !beams.Contains(beam.Id)).Select(beam => beam.Id).ToHashSet() };
        }

        if (movingBeamPartId is not { } moving || FindBeamPart(moving) is null)
        {
            return PlacingTargets.None;
        }

        var takes = Beams.Where(beam => CanMoveBeamPart(moving, new CreatureElementSelection(CreatureElementKind.Beam, beam.Id), out _)).Select(beam => beam.Id).ToHashSet();
        return new PlacingTargets(takes, new HashSet<int>(), moving)
        {
            RefusedBeams = Beams.Where(beam => !takes.Contains(beam.Id)).Select(beam => beam.Id).ToHashSet(),
        };
    }

    /// <summary>
    /// The sensor or Memory cell as a drop on <paramref name="target"/> would leave it, for its preview:
    /// null when the drop would be refused or keep it where it is, or the part or the beam under
    /// it is gone mid-drag.
    /// </summary>
    public IBeamPartDef? BeamPartMovePreview(int partId, CreatureElementSelection? target) =>
        FindBeamPart(partId) is { } part && target is not null && Exists(target)
        && CanMoveBeamPart(partId, target, out _) && part.BeamId != target.Id
            ? _builder.BeamPartMovedTo(partId, target.Id)
            : null;

    /// <summary>
    /// Moves the sensor or Memory cell dragged onto <paramref name="target"/> there, keeping its id, ports
    /// and settings (<see cref="CreatureBuilder.BeamPartMovedTo"/>), and selects it. A drop on empty
    /// canvas (<paramref name="target"/> null) leaves it and says nothing, as <see cref="PlacePart"/>
    /// does; a refused drop leaves it and shows why as <see cref="PlacementNote"/> at the part it was
    /// dropped on. A part gone mid-drag moves nowhere.
    /// </summary>
    public bool MoveBeamPart(int partId, CreatureElementSelection? target)
    {
        PlacementNote = null;
        if (FindBeamPart(partId) is not { } part || target is null)
        {
            return false;
        }

        if (!CanMoveBeamPart(partId, target, out var reason))
        {
            PlacementNote = new CanvasNote(CanvasNoteKind.Danger, target, reason);
            return false;
        }

        if (part.BeamId != target.Id)
        {
            _history.Change(() => _builder.MoveBeamPart(partId, target.Id, out _));
        }

        SelectPlaced(PartSet.Of(CreatureElementSelection.Of(part)), select: true);
        return true;
    }

    /// <summary>
    /// The one rule for where a sensor or Memory cell goes, placed or moved: on a beam (else
    /// <paramref name="goesOnABeam"/>) the builder lets it mount on (<see cref="CreatureBuilder.CanMountOnBeam"/>).
    /// </summary>
    private bool TakesBeamPart(UiText goesOnABeam, [NotNullWhen(true)] CreatureElementSelection? target, int? movingBeamPartId, [NotNullWhen(false)] out UiText? reason)
    {
        if (target is not { Kind: CreatureElementKind.Beam })
        {
            reason = goesOnABeam;
            return false;
        }

        return _builder.CanMountOnBeam(target.Id, movingBeamPartId, out reason);
    }

    private IBeamPartDef? FindBeamPart(int partId) => _builder.FindPart(partId) as IBeamPartDef;

    private IBeamPartDef ExistingBeamPart(int partId) =>
        FindBeamPart(partId) ?? throw new ArgumentException("Part id must point to an existing sensor or Memory cell.", nameof(partId));
}
