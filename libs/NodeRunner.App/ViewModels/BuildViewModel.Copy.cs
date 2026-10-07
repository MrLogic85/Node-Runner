using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The selection panel's Copy (#937, #990): several selected parts duplicated beside themselves,
/// inside the same Creation. A part copies only with what it sits on: a link with both its joints
/// and a sensor with its beam. A Servo is its joint to the player (#973), so it brings that joint
/// along (#1000). A locked Creation copies no part with brain ports, since a copy would change its
/// model.
/// </summary>
public sealed partial class BuildViewModel
{
    private IReadOnlyList<CanvasNote> _shownCopyBlockers = [];

    /// <summary>Whether enough parts are selected to copy: two or more, locked or not (#990).</summary>
    public bool CanOfferCopy => SelectedPartCount >= 2;

    /// <summary>Whether Copy would duplicate the selection now; when not, the button is dimmed.</summary>
    public bool CanCopySelection => CanOfferCopy && CopyBlockers().Count == 0;

    /// <summary>
    /// A danger note at each selected part a copy cannot take, in id order within each kind: on a
    /// locked Creation a sensor, Servo or Piston for its brain ports; a beam, Spring or Piston
    /// without both joints among <see cref="CopiedJointIds"/>; a sensor without its beam.
    /// </summary>
    public IReadOnlyList<CanvasNote> CopyBlockers()
    {
        var notes = new List<CanvasNote>();
        var joints = CopiedJointIds();
        if (_locked)
        {
            AddBrainPortNotes(CreatureElementKind.Sensor, _selectedSensorIds);
            AddBrainPortNotes(CreatureElementKind.Servo, _selectedServoIds);
            AddBrainPortNotes(CreatureElementKind.Piston, _selectedPistonIds);
        }

        foreach (var beam in Beams)
        {
            AddOpenLinkNote(CreatureElementKind.Beam, beam.Id, beam.NodeA, beam.NodeB);
        }

        foreach (var spring in Springs)
        {
            AddOpenLinkNote(CreatureElementKind.Spring, spring.Id, spring.NodeA, spring.NodeB);
        }

        // A part with brain ports already has its note on a locked Creation.
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

        void AddBrainPortNotes(CreatureElementKind kind, HashSet<int> selected)
        {
            foreach (var id in selected.Order())
            {
                notes.Add(Note(kind, id, UiText.Plain("Locked: would change the model")));
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
    /// selects the originals again. Each copy keeps its settings but not its name; a copied Servo
    /// sits on the copy of its joint, uses the copies of its Fixed and Target links, and leaves a
    /// role empty whose link was not copied. A joint a Servo brought is not selected in the copy. While Copy is dimmed, it instead shows <see cref="CopyBlockers"/> in
    /// <see cref="CanvasNotes"/> until the selection changes or the canvas is touched.
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
            _shownCopyBlockers = blockers;
            OnPropertyChanged(nameof(CanvasNotes));
            return;
        }

        var joints = CopiedJointIds();
        var selectedJoints = _selectedNodeIds.ToHashSet();
        var nodes = Nodes.Where(node => joints.Contains(node.Id)).ToList();
        var beams = Beams.Where(beam => _selectedBeamIds.Contains(beam.Id)).ToList();
        var pistons = Pistons.Where(piston => _selectedPistonIds.Contains(piston.Id)).ToList();
        var springs = Springs.Where(spring => _selectedSpringIds.Contains(spring.Id)).ToList();
        var sensors = Sensors.Where(sensor => _selectedSensorIds.Contains(sensor.Id)).ToList();
        var servos = Servos.Where(servo => _selectedServoIds.Contains(servo.Id)).ToList();
        var offset = CopyOffset(nodes);
        _history.Change(() =>
        {
            ClearSelectionSets();
            var nodeCopies = new Dictionary<int, int>();
            var linkCopies = new Dictionary<int, int>();
            foreach (var node in nodes)
            {
                var position = new Vector2D(node.Position.X + offset.X, node.Position.Y + offset.Y);
                // Clamping too absorbs the rounding in a shortened offset, as in TranslateSelection.
                nodeCopies[node.Id] = _builder.AddNode(BuildArea.Clamp(position, NodeDef.PlainJointRadius));
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

            foreach (var servo in servos)
            {
                var copy = _builder.AddServo(nodeCopies[servo.NodeId], CopyOf(servo.FixedLinkId), CopyOf(servo.TargetLinkId));
                _selectedServoIds.Add(CopySettings(servo.Id, copy));
            }

            int? CopyOf(int? linkId) => linkId is { } id && linkCopies.TryGetValue(id, out var copy) ? copy : null;
        }, Selection);
        NotifySelectionChanged();
        RaiseAnatomyChanged();
    }

    // The selected joints and the joints under selected Servos.
    private HashSet<int> CopiedJointIds() =>
        [.. _selectedNodeIds, .. Servos.Where(servo => _selectedServoIds.Contains(servo.Id)).Select(servo => servo.NodeId)];

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
    private static Vector2D CopyOffset(IReadOnlyList<NodeDef> nodes)
    {
        var joints = nodes.Select(node => (node.Position, NodeDef.PlainJointRadius)).ToList();
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
