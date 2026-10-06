using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The selection panel's Copy (#937): several selected joints, beams and Springs duplicated beside
/// themselves, inside the same Creation. Parts with brain ports are never copied, since a copy would
/// change the network, and nor is a link without both of its joints.
/// </summary>
public sealed partial class BuildViewModel
{
    private IReadOnlyList<CanvasNote> _shownCopyBlockers = [];

    /// <summary>Whether the selection panel offers Copy: two or more parts, on a Creation that is not locked.</summary>
    public bool CanOfferCopy => !_moveOnly && SelectedPartCount >= 2;

    /// <summary>Whether Copy would duplicate the selection now; when not, the button is dimmed.</summary>
    public bool CanCopySelection => CanOfferCopy && CopyBlockers().Count == 0;

    /// <summary>
    /// A danger note at each selected part a copy cannot take, in id order within each kind: a sensor,
    /// Servo or Piston for its brain ports, and a beam or Spring without both joints selected.
    /// </summary>
    public IReadOnlyList<CanvasNote> CopyBlockers()
    {
        var notes = new List<CanvasNote>();
        AddBrainPortNotes(CreatureElementKind.Sensor, _selectedSensorIds);
        AddBrainPortNotes(CreatureElementKind.Servo, _selectedServoIds);
        AddBrainPortNotes(CreatureElementKind.Piston, _selectedPistonIds);
        foreach (var beam in Beams)
        {
            AddOpenLinkNote(CreatureElementKind.Beam, beam.Id, beam.NodeA, beam.NodeB);
        }

        foreach (var spring in Springs)
        {
            AddOpenLinkNote(CreatureElementKind.Spring, spring.Id, spring.NodeA, spring.NodeB);
        }

        return notes;

        void AddBrainPortNotes(CreatureElementKind kind, HashSet<int> selected)
        {
            foreach (var id in selected.Order())
            {
                notes.Add(Note(kind, id, UiText.Plain("Would change the brain")));
            }
        }

        void AddOpenLinkNote(CreatureElementKind kind, int id, int nodeA, int nodeB)
        {
            if (SelectedSet(kind).Contains(id) && !(_selectedNodeIds.Contains(nodeA) && _selectedNodeIds.Contains(nodeB)))
            {
                notes.Add(Note(kind, id, UiText.Plain("Select both its joints")));
            }
        }

        static CanvasNote Note(CreatureElementKind kind, int id, UiText text) =>
            new(CanvasNoteKind.Danger, new CreatureElementSelection(kind, id), text);
    }

    /// <summary>
    /// Duplicates the selection one grid step aside and selects the copy, as one undo step that
    /// selects the originals again. Each copy keeps its settings but not its name. While Copy is
    /// dimmed, it instead shows <see cref="CopyBlockers"/> in <see cref="CanvasNotes"/> until the
    /// selection changes.
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

        var nodes = Nodes.Where(node => _selectedNodeIds.Contains(node.Id)).ToList();
        var beams = Beams.Where(beam => _selectedBeamIds.Contains(beam.Id)).ToList();
        var springs = Springs.Where(spring => _selectedSpringIds.Contains(spring.Id)).ToList();
        var offset = CopyOffset(nodes);
        _history.Change(() =>
        {
            ClearSelectionSets();
            var copies = new Dictionary<int, int>();
            foreach (var node in nodes)
            {
                var position = new Vector2D(node.Position.X + offset.X, node.Position.Y + offset.Y);
                // Clamping too absorbs the rounding in a shortened offset, as in TranslateSelection.
                copies[node.Id] = _builder.AddNode(BuildArea.Clamp(position, NodeDef.PlainJointRadius));
                _selectedNodeIds.Add(copies[node.Id]);
            }

            foreach (var beam in beams)
            {
                _selectedBeamIds.Add(_builder.AddBeam(copies[beam.NodeA], copies[beam.NodeB]));
            }

            foreach (var spring in springs)
            {
                var copy = _builder.AddSpring(copies[spring.NodeA], copies[spring.NodeB]);
                foreach (var parameter in _builder.ParametersOf(spring.Id))
                {
                    _builder.SetParameter(copy, parameter, _builder.ParameterValue(spring.Id, parameter));
                }

                _selectedSpringIds.Add(copy);
            }
        }, Selection);
        NotifySelectionChanged();
        RaiseAnatomyChanged();
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
