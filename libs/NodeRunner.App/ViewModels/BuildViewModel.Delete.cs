using NodeRunner.App.Builders;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The selection's Delete: the selected parts and what goes with them, refused on a locked Creation
/// when it would change the model (#896, #987).
/// </summary>
public sealed partial class BuildViewModel
{
    /// <summary>
    /// Why Delete cannot remove the selection now, or null when it can (#896). A locked Creation
    /// refuses a delete that would change its model: one that takes a part with brain ports, also by
    /// cascade (a joint's Piston or Servo, a beam's sensor), or clears a Servo's Fixed or Target link,
    /// after which it could no longer train (<c>docs/SAVE_FORMAT.md</c> → <c>servos[]</c>).
    /// </summary>
    public UiText? DeleteLockedReason => DeleteRefusalNotes().Count > 0 ? LockedReason : null;

    /// <summary>
    /// Deletes the selection and what goes with it, as one undo step. While <see cref="DeleteLockedReason"/>
    /// says why not, it deletes nothing and instead shows a note on each part that blocks it in
    /// <see cref="CanvasNotes"/> (#987).
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

        // A Servo's joint goes too when the delete takes its links and leaves none (#973): to the
        // player the Servo is that joint, so clearing an area must not leave a bare joint behind.
        var servoJoints = SelectedServoJoints().ToDictionary(joint => joint, joint => _builder.LinksAt(joint).Count);
        _history.Change(() =>
        {
            RemoveParts(_builder, Selection);

            foreach (var (joint, linksBefore) in servoJoints)
            {
                if (linksBefore > 0 && Exists(new CreatureElementSelection(CreatureElementKind.Node, joint)) && _builder.LinksAt(joint).Count == 0)
                {
                    _builder.RemoveNode(joint);
                }
            }

            // Within the step: its Undo row refresh must find no deleted part still selected.
            ClearSelectionSets();
        }, Selection);
        NotifySelectionChanged();
        RaiseAnatomyChanged();
    }

    // On a locked Creation, a note on each part that makes the delete change the model, in id order
    // within each kind: each part with brain ports it removes, and each kept Servo whose Fixed or
    // Target link it clears. It tries the delete on a copy of the body, so every cascade counts
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

        var keptServos = after.Servos.Select(ServoLinks).ToHashSet();
        foreach (var servo in before.Servos)
        {
            if (!keptServos.Contains(ServoLinks(servo)))
            {
                AddNote(CreatureElementKind.Servo, servo.Id);
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

        return notes;

        static (int, int?, int?) ServoLinks(ServoDef servo) => (servo.Id, servo.FixedLinkId, servo.TargetLinkId);
        void AddNote(CreatureElementKind kind, int id) =>
            notes.Add(new(CanvasNoteKind.Danger, new CreatureElementSelection(kind, id), _wouldChangeModelNote));
    }

    private static void RemoveParts(CreatureBuilder builder, PartSet parts)
    {
        // Parts first, so none is already gone with a deleted beam or joint.
        foreach (var sensorId in parts.Sensors)
        {
            builder.RemoveSensor(sensorId);
        }

        foreach (var pistonId in parts.Pistons)
        {
            builder.RemovePiston(pistonId);
        }

        foreach (var servoId in parts.Servos)
        {
            builder.RemoveServo(servoId);
        }

        foreach (var springId in parts.Springs)
        {
            builder.RemoveSpring(springId);
        }

        foreach (var beamId in parts.Beams)
        {
            builder.RemoveBeam(beamId);
        }

        foreach (var nodeId in parts.Nodes)
        {
            builder.RemoveNode(nodeId);
        }
    }
}
