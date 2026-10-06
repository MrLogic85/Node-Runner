using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The selection half of <see cref="BuildViewModel"/>: which parts are selected (#704), selecting
/// and clearing them, and the move, turn and scale of the selected joints in any tool.
/// </summary>
public sealed partial class BuildViewModel
{
    /// <summary>The smallest factor one Scale drag can shrink a selection by.</summary>
    public const double MinSelectionScale = 0.25;

    /// <summary>The largest factor one Scale drag can grow a selection by.</summary>
    public const double MaxSelectionScale = 4;

    private readonly HashSet<int> _selectedNodeIds = [];
    private readonly HashSet<int> _selectedBeamIds = [];
    private readonly HashSet<int> _selectedSensorIds = [];
    private readonly HashSet<int> _selectedServoIds = [];
    private readonly HashSet<int> _selectedPistonIds = [];
    private readonly HashSet<int> _selectedSpringIds = [];

    public int SelectedNodeCount => _selectedNodeIds.Count;

    public int SelectedBeamCount => _selectedBeamIds.Count;

    public int SelectedSensorCount => _selectedSensorIds.Count;

    public int SelectedServoCount => _selectedServoIds.Count;

    public int SelectedPistonCount => _selectedPistonIds.Count;

    public int SelectedSpringCount => _selectedSpringIds.Count;

    public int SelectedPartCount => SelectedNodeCount + SelectedBeamCount + SelectedSensorCount + SelectedServoCount + SelectedPistonCount + SelectedSpringCount;

    public int? SingleSelectedNodeId => Single(_selectedNodeIds);

    public int? SingleSelectedBeamId => Single(_selectedBeamIds);

    public int? SingleSelectedSensorId => Single(_selectedSensorIds);

    public int? SingleSelectedServoId => Single(_selectedServoIds);

    public int? SingleSelectedPistonId => Single(_selectedPistonIds);

    public int? SingleSelectedSpringId => Single(_selectedSpringIds);

    /// <summary>A copy of everything selected (#704).</summary>
    public PartSet Selection => new(
        _selectedNodeIds.ToHashSet(),
        _selectedBeamIds.ToHashSet(),
        _selectedSensorIds.ToHashSet(),
        _selectedServoIds.ToHashSet(),
        _selectedPistonIds.ToHashSet(),
        _selectedSpringIds.ToHashSet());

    public IReadOnlyCollection<int> SelectedNodeIds => _selectedNodeIds;

    /// <summary>Adds the part to the selection, or removes it if it is already selected (#704).</summary>
    public void ToggleSelected(CreatureElementSelection element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!Exists(element))
        {
            throw new ArgumentException($"No {element.Kind} {element.Id}.", nameof(element));
        }

        var set = SelectedSet(element.Kind);
        if (!set.Add(element.Id))
        {
            set.Remove(element.Id);
        }

        SelectionChanged();
    }

    private IEnumerable<int> SelectedPartIds() =>
        _selectedNodeIds.Concat(_selectedBeamIds).Concat(_selectedSensorIds).Concat(_selectedServoIds).Concat(_selectedPistonIds).Concat(_selectedSpringIds);

    public void ClearSelection()
    {
        if (SelectedPartCount == 0)
        {
            return;
        }

        ClearSelectionSets();
        NotifySelectionChanged();
        RaiseAnatomyChanged();
    }

    public void ReplaceSelection(IEnumerable<int> nodeIds)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);
        ReplaceSelection(PartSet.None with { Nodes = nodeIds.ToHashSet() });
    }

    /// <summary>Selects exactly the <paramref name="parts"/> that still exist.</summary>
    public void ReplaceSelection(PartSet parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ClearSelectionSets();
        foreach (var kind in Enum.GetValues<CreatureElementKind>())
        {
            SelectedSet(kind).UnionWith(parts.SetOf(kind).Where(id => Exists(new CreatureElementSelection(kind, id))));
        }

        SelectionChanged();
    }

    private HashSet<int> SelectedSet(CreatureElementKind kind) => kind switch
    {
        CreatureElementKind.Node => _selectedNodeIds,
        CreatureElementKind.Beam => _selectedBeamIds,
        CreatureElementKind.Sensor => _selectedSensorIds,
        CreatureElementKind.Servo => _selectedServoIds,
        CreatureElementKind.Piston => _selectedPistonIds,
        CreatureElementKind.Spring => _selectedSpringIds,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private void ClearSelectionSets()
    {
        _selectedNodeIds.Clear();
        _selectedBeamIds.Clear();
        _selectedSensorIds.Clear();
        _selectedServoIds.Clear();
        _selectedPistonIds.Clear();
        _selectedSpringIds.Clear();
    }

    private int? Single(HashSet<int> set) => SelectedPartCount == 1 && set.Count == 1 ? set.First() : null;

    private List<int> SelectedServoJoints() =>
        _builder.Servos.Where(servo => _selectedServoIds.Contains(servo.Id)).Select(servo => servo.NodeId).ToList();

    // Drops the ids the body no longer has. Changing a Servo's links gives it a new id (#911, #849),
    // so the Servo now on each of servoJoints, the joints of the Servos selected before, stays selected.
    private void PruneSelection(List<int> servoJoints)
    {
        foreach (var kind in Enum.GetValues<CreatureElementKind>())
        {
            SelectedSet(kind).RemoveWhere(id => !Exists(new CreatureElementSelection(kind, id)));
        }

        _selectedServoIds.UnionWith(_builder.Servos.Where(servo => servoJoints.Contains(servo.NodeId)).Select(servo => servo.Id));
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
        if (_selectedNodeIds.Count == 0)
        {
            throw new InvalidOperationException("Nothing is selected.");
        }

        var positions = _selectedNodeIds.ToDictionary(id => id, id => NodeById(id).Position);
        pivot ??= new Vector2D(
            (positions.Values.Min(p => p.X) + positions.Values.Max(p => p.X)) / 2,
            (positions.Values.Min(p => p.Y) + positions.Values.Max(p => p.Y)) / 2);
        return new SelectionSnapshot(positions, pivot.Value);
    }

    /// <summary>Moves the snapshot's joints by <paramref name="delta"/>, shortened so the whole group stays inside <see cref="BuildArea"/>.</summary>
    public void TranslateSelection(SelectionSnapshot start, Vector2D delta)
    {
        ArgumentNullException.ThrowIfNull(start);
        foreach (var (id, position) in start.Positions)
        {
            var moved = new Vector2D(position.X + delta.X, position.Y + delta.Y);
            var allowed = BuildArea.Clamp(moved, NodeRadius(id));
            delta = new Vector2D(delta.X + allowed.X - moved.X, delta.Y + allowed.Y - moved.Y);
        }

        // Clamping each joint too absorbs the rounding in the shortened delta.
        PlaceSelection(start, (id, position) =>
            BuildArea.Clamp(new Vector2D(position.X + delta.X, position.Y + delta.Y), NodeRadius(id)));
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
        OnPropertyChanged(nameof(SelectedNodeCount));
        OnPropertyChanged(nameof(SelectedBeamCount));
        OnPropertyChanged(nameof(SelectedPartCount));
        OnPropertyChanged(nameof(SingleSelectedNodeId));
        OnPropertyChanged(nameof(SingleSelectedBeamId));
        OnPropertyChanged(nameof(SelectedSensorCount));
        OnPropertyChanged(nameof(SingleSelectedSensorId));
        OnPropertyChanged(nameof(SelectedServoCount));
        OnPropertyChanged(nameof(SingleSelectedServoId));
        OnPropertyChanged(nameof(SelectedPistonCount));
        OnPropertyChanged(nameof(SingleSelectedPistonId));
        OnPropertyChanged(nameof(SelectedSpringCount));
        OnPropertyChanged(nameof(SingleSelectedSpringId));
    }
}
