using NodeRunner.App.Lifecycle;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The notes half of <see cref="BuildViewModel"/>: the messages Build shows in the drawing beside
/// the parts they are about, and the taps that bring them up and hide them.
/// </summary>
public sealed partial class BuildViewModel
{
    /// <summary>
    /// The messages Build shows in the drawing, each beside the part it is about: why the last
    /// dropped part was refused, each loose joint and hinge part missing a side
    /// <see cref="ShowTrainingBlockers"/> pointed at, each separate piece's joint nearest
    /// another piece and each part a dimmed Copy or a locked Delete pointed at, then each beam or link too
    /// short to train. Listed most important first:
    /// notes that would overlap stack, the first listed nearest its part.
    /// </summary>
    public IReadOnlyList<CanvasNote> CanvasNotes()
    {
        var notes = new List<CanvasNote>();
        if (_placementNote is { } placement && Exists(placement.Target))
        {
            notes.Add(placement);
        }

        foreach (var node in Nodes)
        {
            if (_shownBlockers.Contains(new CreatureElementSelection(CreatureElementKind.Node, node.Id)) && IsLoose(node.Id))
            {
                notes.Add(NotConnected(node.Id));
            }
        }

        if (_showPieces && Pieces() is { Count: > 1 } pieces)
        {
            notes.AddRange(pieces.Select(piece => NotConnected(NearestOtherPiece(piece, pieces))));
        }

        notes.AddRange(_shownRefusalNotes.Where(note => Exists(note.Target)));

        foreach (var beam in Beams)
        {
            AddTooShortNote(CreatureElementKind.Beam, beam.Id, beam.NodeA, beam.NodeB);
        }

        foreach (var part in HingeParts.Where(part => _shownBlockers.Contains(new CreatureElementSelection(part.Kind, part.Id)) && CreatureReadiness.IsMissingALink(part)))
        {
            var text = ServoNeedsTwoSides(part.NodeId)
                ? NeedsTwoSidesReason(part.Kind)
                : (part.FixedLinkId is null, part.TargetLinkId is null) switch
                {
                    (true, true) => UiText.Plain("Pick Fixed and Target parts"),
                    (true, false) => UiText.Plain("Pick a Fixed part"),
                    _ => UiText.Plain("Pick a Target part"),
                };
            notes.Add(new CanvasNote(CanvasNoteKind.Danger, new CreatureElementSelection(part.Kind, part.Id), text));
        }

        foreach (var piston in Pistons)
        {
            AddTooShortNote(CreatureElementKind.Piston, piston.Id, piston.NodeA, piston.NodeB);
        }

        foreach (var spring in Springs)
        {
            AddTooShortNote(CreatureElementKind.Spring, spring.Id, spring.NodeA, spring.NodeB);
        }

        return notes;

        // The piece's joint nearest another piece, where a joining link would go; the first on a tie.
        int NearestOtherPiece(IReadOnlyList<int> piece, IReadOnlyList<IReadOnlyList<int>> pieces)
        {
            var nearest = piece[0];
            var nearestDistanceSquared = double.PositiveInfinity;
            foreach (var id in piece)
            {
                var position = Nodes[NodeIndexOf(id)].Position;
                foreach (var other in pieces)
                {
                    if (other == piece)
                    {
                        continue;
                    }

                    foreach (var otherId in other)
                    {
                        var otherPosition = Nodes[NodeIndexOf(otherId)].Position;
                        var dx = position.X - otherPosition.X;
                        var dy = position.Y - otherPosition.Y;
                        var distanceSquared = (dx * dx) + (dy * dy);
                        if (distanceSquared < nearestDistanceSquared)
                        {
                            nearestDistanceSquared = distanceSquared;
                            nearest = id;
                        }
                    }
                }
            }

            return nearest;
        }

        static CanvasNote NotConnected(int nodeId) =>
            new(CanvasNoteKind.Danger, new CreatureElementSelection(CreatureElementKind.Node, nodeId), UiText.Plain("Not connected"));

        void AddTooShortNote(CreatureElementKind kind, int id, int nodeA, int nodeB)
        {
            if (CreatureReadiness.IsTooShort(kind, Nodes[NodeIndexOf(nodeA)], Nodes[NodeIndexOf(nodeB)], JointMotors))
            {
                notes.Add(new CanvasNote(CanvasNoteKind.Danger, new CreatureElementSelection(kind, id), UiText.Plain("Too short")));
            }
        }
    }

    /// <summary>Why the last part dropped from the tray was refused, shown at the part it was dropped on; null when there is none.</summary>
    public CanvasNote? PlacementNote
    {
        get => _placementNote;
        private set
        {
            if (_placementNote == value)
            {
                return;
            }

            _placementNote = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Hides <see cref="PlacementNote"/> once it has been read.</summary>
    public void DismissPlacementNote() => PlacementNote = null;

    /// <summary>
    /// Hides the notes a tap brought up, on the next canvas touch: <see cref="PlacementNote"/>,
    /// <see cref="ShowTrainingBlockers"/>' notes and a dimmed Copy's or a locked Delete's notes. Too-short parts' notes
    /// stay, as they mark the drawing itself.
    /// </summary>
    public void DismissTapNotes()
    {
        PlacementNote = null;
        if (_shownBlockers.Count == 0 && !_showPieces && _shownRefusalNotes.Count == 0)
        {
            return;
        }

        _shownBlockers.Clear();
        _showPieces = false;
        _shownRefusalNotes = [];
        OnPropertyChanged(nameof(CanvasNotes));
    }

    private void ShowRefusalNotes(IReadOnlyList<CanvasNote> notes)
    {
        _shownRefusalNotes = notes;
        OnPropertyChanged(nameof(CanvasNotes));
    }

    /// <summary>
    /// Answers a tap on the dimmed play button: every joint loose now gets a "Not connected"
    /// note in <see cref="CanvasNotes"/>, kept until it is joined or removed, and so does every
    /// piece until the creation is one piece and every hinge part missing a side until it has
    /// both; the next canvas touch hides them all (<see cref="DismissTapNotes"/>). Too-short
    /// parts already have theirs. A blocker that appears later waits for the next tap.
    /// </summary>
    public void ShowTrainingBlockers()
    {
        _showPieces = Pieces().Count > 1;
        _shownBlockers.Clear();
        _shownBlockers.UnionWith(Nodes.Where(node => IsLoose(node.Id)).Select(node => new CreatureElementSelection(CreatureElementKind.Node, node.Id)));
        _shownBlockers.UnionWith(HingeParts.Where(CreatureReadiness.IsMissingALink).Select(part => new CreatureElementSelection(part.Kind, part.Id)));

        OnPropertyChanged(nameof(CanvasNotes));
    }
}
