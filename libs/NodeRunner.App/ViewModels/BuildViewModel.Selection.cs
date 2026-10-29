using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The selection half of <see cref="BuildViewModel"/>: which parts are selected, selecting
/// and clearing them, and the move, turn and scale of the selected joints in any tool.
/// </summary>
public sealed partial class BuildViewModel
{
    /// <summary>The smallest factor one Scale drag can shrink a selection by.</summary>
    public const double MinSelectionScale = 0.25;

    /// <summary>The largest factor one Scale drag can grow a selection by.</summary>
    public const double MaxSelectionScale = 4;

    private readonly HashSet<CreatureElementSelection> _selected = [];

    /// <summary>How many joints the selection moves: <see cref="SelectedNodeIds"/>.</summary>
    public int SelectedNodeCount => SelectedNodeIds.Count;

    public int SelectedPartCount => _selected.Count;

    /// <summary>The one selected part, or null unless exactly one part is selected.</summary>
    public CreatureElementSelection? SingleSelected => _selected.Count == 1 ? _selected.First() : null;

    /// <summary>A copy of everything selected.</summary>
    public PartSet Selection => new(_selected);

    /// <summary>
    /// The joints the selection moves: the selected joints and the joints under selected joint
    /// parts, since to the player a joint part is its joint.
    /// </summary>
    public IReadOnlyCollection<int> SelectedNodeIds => SelectedIds(CreatureElementKind.Node).Union(SelectedJointPartJoints()).ToHashSet();

    /// <summary>The id of the one selected part if it is a <paramref name="kind"/>, else null.</summary>
    public int? SingleSelectedId(CreatureElementKind kind) => SingleSelected is { } part && part.Kind == kind ? part.Id : null;

    /// <summary>Adds the part to the selection, or removes it if it is already selected.</summary>
    public void ToggleSelected(CreatureElementSelection element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!Exists(element))
        {
            throw new ArgumentException($"No {element.Kind} {element.Id}.", nameof(element));
        }

        if (!_selected.Add(element))
        {
            _selected.Remove(element);
        }

        SelectionChanged();
    }

    /// <summary>
    /// A tap on joint <paramref name="nodeId"/>: a bare joint is added or removed. A joint
    /// with parts steps down its stack (<see cref="JointPartsAt"/>, top → down): with none of them
    /// selected it adds the top one; otherwise it swaps the deepest selected one for the next, or,
    /// past the last, removes them all.
    /// </summary>
    public void ToggleJointSelected(int nodeId)
    {
        var parts = JointPartsAt(nodeId);
        if (parts.Count == 0)
        {
            ToggleSelected(new CreatureElementSelection(CreatureElementKind.Node, nodeId));
            return;
        }

        var deepest = parts.ToList().FindLastIndex(_selected.Contains);
        _selected.ExceptWith(parts);
        if (deepest + 1 < parts.Count)
        {
            _selected.Add(parts[deepest + 1]);
        }

        SelectionChanged();
    }

    // Ids of every kind mixed, which the builder's parameter calls take, since a part id is unique across kinds.
    private IEnumerable<int> SelectedPartIds() => Selection.Parts.Select(part => part.Id);

    /// <summary>Whether that part is selected, without copying the <see cref="Selection"/>.</summary>
    public bool IsSelected(CreatureElementKind kind, int id) => _selected.Contains(new CreatureElementSelection(kind, id));

    // The joints under the selected joint parts, each once, though several parts of a stack can be selected.
    private HashSet<int> SelectedJointPartJoints() =>
        [.. JointParts.Where(part => _selected.Contains(CreatureElementSelection.Of(part))).Select(part => part.NodeId)];

    private IEnumerable<int> SelectedIds(CreatureElementKind kind) => _selected.Where(part => part.Kind == kind).Select(part => part.Id);

    public void ClearSelection()
    {
        if (SelectedPartCount == 0)
        {
            return;
        }

        _selected.Clear();
        NotifySelectionChanged();
        RaiseAnatomyChanged();
    }

    public void ReplaceSelection(IEnumerable<int> nodeIds)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);
        ReplaceSelection(new PartSet(nodeIds.Select(id => new CreatureElementSelection(CreatureElementKind.Node, id))));
    }

    /// <summary>Selects exactly the <paramref name="parts"/> that still exist.</summary>
    public void ReplaceSelection(PartSet parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        _selected.Clear();
        _selected.UnionWith(parts.Parts.Where(Exists));
        SelectionChanged();
    }

    // Drops the parts the body no longer has.
    private void PruneSelection() => _selected.RemoveWhere(part => !Exists(part));

    // A joint part stands in for its joint, also when Undo or Redo brings one back under a kept
    // joint selection: the top of its stack, as a tap selects it.
    private void SelectJointPartsInsteadOfTheirJoints()
    {
        foreach (var nodeId in SelectedIds(CreatureElementKind.Node).ToList())
        {
            if (JointPartsAt(nodeId) is [var top, ..])
            {
                _selected.Remove(new CreatureElementSelection(CreatureElementKind.Node, nodeId));
                _selected.Add(top);
            }
        }
    }

    private void SelectionChanged()
    {
        NotifySelectionChanged();
        RaiseAnatomyChanged();
    }

    /// <summary>
    /// The selected joints' positions and pivot, for a selection drag to transform: the given
    /// <paramref name="pivot"/>, or else the middle of the joints' centres.
    /// </summary>
    public SelectionSnapshot SnapshotSelection(Vector2D? pivot = null)
    {
        var joints = SelectedNodeIds;
        if (joints.Count == 0)
        {
            throw new InvalidOperationException("Nothing is selected.");
        }

        var positions = joints.ToDictionary(id => id, id => NodeById(id).Position);
        pivot ??= new Vector2D(
            (positions.Values.Min(p => p.X) + positions.Values.Max(p => p.X)) / 2,
            (positions.Values.Min(p => p.Y) + positions.Values.Max(p => p.Y)) / 2);
        return new SelectionSnapshot(positions, pivot.Value);
    }

    /// <summary>
    /// Moves the snapshot's joints by <paramref name="delta"/>, changed to put one joint on the grid
    /// when <see cref="SnapToGrid"/>, then shortened so the whole group stays inside <see cref="BuildArea"/>.
    /// </summary>
    public void TranslateSelection(SelectionSnapshot start, Vector2D delta)
    {
        ArgumentNullException.ThrowIfNull(start);
        if (SnapToGrid)
        {
            delta = SnappedToGrid(start, delta);
        }

        delta = ShortenedToBuildArea([.. start.Positions.Select(pair => (pair.Value, NodeRadius(pair.Key)))], delta);

        // Clamping each joint too absorbs the rounding in the shortened delta.
        PlaceSelection(start, (id, position) =>
            BuildArea.Clamp(new Vector2D(position.X + delta.X, position.Y + delta.Y), NodeRadius(id)));
    }

    // The delta changed least so that one joint lands on the grid: the joint nearest a grid point
    // once moved, the lowest id on a tie. The group keeps its shape.
    private Vector2D SnappedToGrid(SelectionSnapshot start, Vector2D delta)
    {
        var snapped = delta;
        var nearest = double.PositiveInfinity;
        foreach (var (id, position) in start.Positions.OrderBy(pair => pair.Key))
        {
            var moved = new Vector2D(position.X + delta.X, position.Y + delta.Y);
            var onGrid = OnGrid(moved, NodeRadius(id));
            var distance = Math.Pow(onGrid.X - moved.X, 2) + Math.Pow(onGrid.Y - moved.Y, 2);
            if (distance < nearest)
            {
                nearest = distance;
                snapped = new Vector2D(onGrid.X - position.X, onGrid.Y - position.Y);
            }
        }

        return snapped;
    }

    // The delta shortened so every joint, moved by it together, stays inside BuildArea.
    private static Vector2D ShortenedToBuildArea(IReadOnlyList<(Vector2D Position, double Radius)> joints, Vector2D delta)
    {
        foreach (var (position, radius) in joints)
        {
            var moved = new Vector2D(position.X + delta.X, position.Y + delta.Y);
            var allowed = BuildArea.Clamp(moved, radius);
            delta = new Vector2D(delta.X + allowed.X - moved.X, delta.Y + allowed.Y - moved.Y);
        }

        return delta;
    }

    /// <summary>
    /// Turns the snapshot's joints <paramref name="radians"/> about its pivot; a turn that would
    /// leave <see cref="BuildArea"/> is ignored. Returns whether the joints turned.
    /// </summary>
    public bool RotateSelection(SelectionSnapshot start, double radians)
    {
        ArgumentNullException.ThrowIfNull(start);
        if (!double.IsFinite(radians))
        {
            throw new ArgumentOutOfRangeException(nameof(radians));
        }

        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var pivot = start.Pivot;
        return PlaceSelection(start, (_, position) =>
        {
            var dx = position.X - pivot.X;
            var dy = position.Y - pivot.Y;
            return new Vector2D(pivot.X + (dx * cos) - (dy * sin), pivot.Y + (dx * sin) + (dy * cos));
        });
    }

    /// <summary>
    /// Spreads the snapshot's joints from its pivot by <paramref name="factor"/>, clamped to
    /// <see cref="MinSelectionScale"/>..<see cref="MaxSelectionScale"/> so it never collapses or
    /// reflects; a scale that would leave <see cref="BuildArea"/> is ignored. A locked creation
    /// scales too: like a move, it only changes beam lengths, not the parts.
    /// </summary>
    public void ScaleSelection(SelectionSnapshot start, double factor)
    {
        ArgumentNullException.ThrowIfNull(start);
        if (!double.IsFinite(factor))
        {
            throw new ArgumentOutOfRangeException(nameof(factor));
        }

        factor = Math.Clamp(factor, MinSelectionScale, MaxSelectionScale);
        var pivot = start.Pivot;
        PlaceSelection(start, (_, position) => new Vector2D(
            pivot.X + ((position.X - pivot.X) * factor),
            pivot.Y + ((position.Y - pivot.Y) * factor)));
    }

    /// <summary>Puts the snapshot's joints back where they were.</summary>
    public void RestoreSelection(SelectionSnapshot start)
    {
        ArgumentNullException.ThrowIfNull(start);
        PlaceSelection(start, (_, position) => position, keepInBuildArea: false);
    }

    private bool PlaceSelection(SelectionSnapshot start, Func<int, Vector2D, Vector2D> place, bool keepInBuildArea = true)
    {
        var placed = start.Positions.ToDictionary(entry => entry.Key, entry => place(entry.Key, entry.Value));
        if (keepInBuildArea && placed.Any(entry => BuildArea.Clamp(entry.Value, NodeRadius(entry.Key)) != entry.Value))
        {
            return false;
        }

        _history.Change(() =>
        {
            foreach (var (id, position) in placed)
            {
                _builder.MoveNode(id, position);
            }
        });
        RaiseAnatomyChanged();
        return true;
    }

    private void NotifySelectionChanged()
    {
        _shownRefusalNotes = [];
        if (SelectedPartCount > 0)
        {
            // A selection replaces the tray, so a pick it would hide is dropped, not kept out of sight.
            ClearPickedPart();
        }

        OnPropertyChanged(nameof(SelectedNodeCount));
        OnPropertyChanged(nameof(SelectedPartCount));
        OnPropertyChanged(nameof(SingleSelected));
    }
}
