using NodeRunner.App.Builders;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The selection's Delete: the selected parts and what goes with them, refused on a locked Creation
/// when it would change the network.
/// </summary>
public sealed partial class BuildViewModel
{
    /// <summary>
    /// Why Delete cannot remove the selection now, or null when it can. A locked Creation
    /// refuses a delete that would change its network: one that takes a part with network ports, also by
    /// cascade (a joint's Piston, motor, Touch sensor or Angle sensor, a beam's sensor), or clears a motor's or an
    /// Angle sensor's Fixed or Target link,
    /// after which it could no longer train (<c>docs/SAVE_FORMAT.md</c> → "<c>creation.json</c>"). A Wheel,
    /// a Rubber and a Brake have no ports, so they can go, and so can a Brake's side.
    /// </summary>
    public UiText? DeleteLockedReason => DeleteRefusalNotes().Count > 0 ? LockedReason : null;

    /// <summary>
    /// Deletes the selection and what goes with it, as one undo step. While <see cref="DeleteLockedReason"/>
    /// says why not, it deletes nothing and instead shows a note on each part that blocks it in
    /// <see cref="CanvasNotes"/>.
    /// </summary>
    public void DeleteSelectedParts()
    {
        if (SelectedPartCount == 0)
        {
            return;
        }

        var refusalNotes = DeleteRefusalNotes();
        if (refusalNotes.Count > 0)
        {
            ShowRefusalNotes(refusalNotes);
            return;
        }

        // A joint part's joint goes too when the delete takes its links and its parts and leaves none:
        // to the player its parts are that joint, so clearing an area must not leave a
        // bare joint behind. A joint that had no links stays.
        var partJoints = SelectedJointPartJoints().ToDictionary(joint => joint, joint => _builder.LinksAt(joint).Count);
        _history.Change(() =>
        {
            RemoveParts(_builder, Selection);

            foreach (var (joint, linksBefore) in partJoints)
            {
                if (linksBefore > 0 && Exists(new CreatureElementSelection(CreatureElementKind.Node, joint)) && _builder.LinksAt(joint).Count == 0
                    && !CreatureDef.HasJointPart(joint, _builder.JointParts))
                {
                    _builder.RemoveNode(joint);
                }
            }

            // Within the step: its Undo row refresh must find no deleted part still selected.
            _selected.Clear();
        }, Selection);
        NotifySelectionChanged();
        RaiseAnatomyChanged();
    }

    // On a locked Creation, a note on each part that makes the delete change the network, in id order
    // within each kind: each part with network ports it removes, and each kept motor or Angle sensor
    // whose Fixed or Target link it clears. It tries the delete on a copy of the body, so every cascade counts
    // exactly as the delete does it. Empty when the delete may go ahead.
    private List<CanvasNote> DeleteRefusalNotes()
    {
        List<CanvasNote> notes = [];
        if (!_locked || SelectedPartCount == 0)
        {
            return notes;
        }

        var before = _builder.Build();
        var trial = new CreatureBuilder(before);
        RemoveParts(trial, Selection);
        var after = trial.Build();

        var keptSensors = after.Sensors.Select(sensor => sensor.Id).ToHashSet();
        foreach (var sensor in before.Sensors)
        {
            if (!keptSensors.Contains(sensor.Id))
            {
                AddNote(CreatureElementKind.Sensor, sensor.Id);
            }
        }

        var keptServos = after.Servos.Select(MotorLinks).ToHashSet();
        foreach (var servo in before.Servos)
        {
            if (!keptServos.Contains(MotorLinks(servo)))
            {
                AddNote(CreatureElementKind.Servo, servo.Id);
            }
        }

        var keptVelocityMotors = after.VelocityMotors.Select(MotorLinks).ToHashSet();
        foreach (var motor in before.VelocityMotors)
        {
            if (!keptVelocityMotors.Contains(MotorLinks(motor)))
            {
                AddNote(CreatureElementKind.VelocityMotor, motor.Id);
            }
        }

        var keptSteppers = after.Steppers.Select(MotorLinks).ToHashSet();
        foreach (var stepper in before.Steppers)
        {
            if (!keptSteppers.Contains(MotorLinks(stepper)))
            {
                AddNote(CreatureElementKind.Stepper, stepper.Id);
            }
        }

        var keptAngleSensors = after.AngleSensors.Select(MotorLinks).ToHashSet();
        foreach (var angleSensor in before.AngleSensors)
        {
            if (!keptAngleSensors.Contains(MotorLinks(angleSensor)))
            {
                AddNote(CreatureElementKind.AngleSensor, angleSensor.Id);
            }
        }

        var keptPistons = after.Pistons.Select(piston => piston.Id).ToHashSet();
        foreach (var piston in before.Pistons)
        {
            if (!keptPistons.Contains(piston.Id))
            {
                AddNote(CreatureElementKind.Piston, piston.Id);
            }
        }

        // A Touch sensor has no picture, so its note sits on its joint.
        var keptTouchSensors = after.TouchSensors.Select(touch => touch.Id).ToHashSet();
        foreach (var touch in before.TouchSensors)
        {
            if (!keptTouchSensors.Contains(touch.Id))
            {
                AddNote(CreatureElementKind.Node, touch.NodeId);
            }
        }

        return notes;

        static (int, int?, int?) MotorLinks(IHingePartDef motor) => (motor.Id, motor.FixedLinkId, motor.TargetLinkId);
        void AddNote(CreatureElementKind kind, int id) =>
            notes.Add(new(CanvasNoteKind.Danger, new CreatureElementSelection(kind, id), _wouldChangeNetworkNote));
    }

    private static void RemoveParts(CreatureBuilder builder, PartSet parts)
    {
        // Parts first, so none is already gone with a deleted beam or joint.
        foreach (var sensorId in parts.IdsOf(CreatureElementKind.Sensor))
        {
            builder.RemoveSensor(sensorId);
        }

        foreach (var pistonId in parts.IdsOf(CreatureElementKind.Piston))
        {
            builder.RemovePiston(pistonId);
        }

        foreach (var servoId in parts.IdsOf(CreatureElementKind.Servo))
        {
            builder.RemoveServo(servoId);
        }

        foreach (var velocityMotorId in parts.IdsOf(CreatureElementKind.VelocityMotor))
        {
            builder.RemoveVelocityMotor(velocityMotorId);
        }

        foreach (var stepperId in parts.IdsOf(CreatureElementKind.Stepper))
        {
            builder.RemoveStepper(stepperId);
        }

        foreach (var brakeId in parts.IdsOf(CreatureElementKind.Brake))
        {
            builder.RemoveBrake(brakeId);
        }

        foreach (var angleSensorId in parts.IdsOf(CreatureElementKind.AngleSensor))
        {
            builder.RemoveAngleSensor(angleSensorId);
        }

        foreach (var springId in parts.IdsOf(CreatureElementKind.Spring))
        {
            builder.RemoveSpring(springId);
        }

        foreach (var wheelId in parts.IdsOf(CreatureElementKind.Wheel))
        {
            builder.RemoveWheel(wheelId);
        }

        foreach (var touchSensorId in parts.IdsOf(CreatureElementKind.TouchSensor))
        {
            builder.RemoveTouchSensor(touchSensorId);
        }

        foreach (var rubberId in parts.IdsOf(CreatureElementKind.Rubber))
        {
            builder.RemoveRubber(rubberId);
        }

        foreach (var beamId in parts.IdsOf(CreatureElementKind.Beam))
        {
            builder.RemoveBeam(beamId);
        }

        foreach (var nodeId in parts.IdsOf(CreatureElementKind.Node))
        {
            builder.RemoveNode(nodeId);
        }
    }
}
