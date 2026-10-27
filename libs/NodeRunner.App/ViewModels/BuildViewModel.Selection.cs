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

    private readonly HashSet<int> _selectedNodeIds = [];
    private readonly HashSet<int> _selectedBeamIds = [];
    private readonly HashSet<int> _selectedSensorIds = [];
    private readonly HashSet<int> _selectedServoIds = [];
    private readonly HashSet<int> _selectedPistonIds = [];
    private readonly HashSet<int> _selectedSpringIds = [];
    private readonly HashSet<int> _selectedWheelIds = [];
    private readonly HashSet<int> _selectedTouchSensorIds = [];
    private readonly HashSet<int> _selectedRubberIds = [];
    private readonly HashSet<int> _selectedVelocityMotorIds = [];
    private readonly HashSet<int> _selectedBrakeIds = [];
    private readonly HashSet<int> _selectedAngleSensorIds = [];
    private readonly HashSet<int> _selectedStepperIds = [];

    /// <summary>How many joints the selection moves: <see cref="SelectedNodeIds"/>.</summary>
    public int SelectedNodeCount => SelectedNodeIds.Count;

    public int SelectedBeamCount => _selectedBeamIds.Count;

    public int SelectedSensorCount => _selectedSensorIds.Count;

    public int SelectedServoCount => _selectedServoIds.Count;

    public int SelectedPistonCount => _selectedPistonIds.Count;

    public int SelectedSpringCount => _selectedSpringIds.Count;

    public int SelectedWheelCount => _selectedWheelIds.Count;

    public int SelectedTouchSensorCount => _selectedTouchSensorIds.Count;

    public int SelectedRubberCount => _selectedRubberIds.Count;

    public int SelectedVelocityMotorCount => _selectedVelocityMotorIds.Count;

    public int SelectedBrakeCount => _selectedBrakeIds.Count;

    public int SelectedAngleSensorCount => _selectedAngleSensorIds.Count;

    public int SelectedStepperCount => _selectedStepperIds.Count;

    public int SelectedPartCount => _selectedNodeIds.Count + SelectedBeamCount + SelectedSensorCount + SelectedServoCount + SelectedPistonCount + SelectedSpringCount + SelectedWheelCount + SelectedTouchSensorCount + SelectedRubberCount + SelectedVelocityMotorCount + SelectedBrakeCount + SelectedAngleSensorCount + SelectedStepperCount;

    public int? SingleSelectedNodeId => Single(_selectedNodeIds);

    public int? SingleSelectedBeamId => Single(_selectedBeamIds);

    public int? SingleSelectedSensorId => Single(_selectedSensorIds);

    public int? SingleSelectedServoId => Single(_selectedServoIds);

    public int? SingleSelectedPistonId => Single(_selectedPistonIds);

    public int? SingleSelectedSpringId => Single(_selectedSpringIds);

    public int? SingleSelectedWheelId => Single(_selectedWheelIds);

    public int? SingleSelectedTouchSensorId => Single(_selectedTouchSensorIds);

    public int? SingleSelectedRubberId => Single(_selectedRubberIds);

    public int? SingleSelectedVelocityMotorId => Single(_selectedVelocityMotorIds);

    public int? SingleSelectedBrakeId => Single(_selectedBrakeIds);

    public int? SingleSelectedAngleSensorId => Single(_selectedAngleSensorIds);

    public int? SingleSelectedStepperId => Single(_selectedStepperIds);

    /// <summary>A copy of everything selected.</summary>
    public PartSet Selection => new(
        _selectedNodeIds.ToHashSet(),
        _selectedBeamIds.ToHashSet(),
        _selectedSensorIds.ToHashSet(),
        _selectedServoIds.ToHashSet(),
        _selectedPistonIds.ToHashSet(),
        _selectedSpringIds.ToHashSet(),
        _selectedWheelIds.ToHashSet(),
        _selectedTouchSensorIds.ToHashSet(),
        _selectedRubberIds.ToHashSet(),
        _selectedVelocityMotorIds.ToHashSet(),
        _selectedBrakeIds.ToHashSet(),
        _selectedAngleSensorIds.ToHashSet(),
        _selectedStepperIds.ToHashSet());

    /// <summary>
    /// The joints the selection moves: the selected joints and the joints under selected joint
    /// parts, Servos, Velocity motors, Steppers, Brakes, Angle sensors, Wheels, Touch sensors and Rubbers, since to the player a joint part is its joint.
    /// </summary>
    public IReadOnlyCollection<int> SelectedNodeIds => _selectedServoIds.Count == 0 && _selectedWheelIds.Count == 0 && _selectedTouchSensorIds.Count == 0 && _selectedRubberIds.Count == 0 && _selectedVelocityMotorIds.Count == 0 && _selectedBrakeIds.Count == 0 && _selectedAngleSensorIds.Count == 0 && _selectedStepperIds.Count == 0
        ? _selectedNodeIds
        : _selectedNodeIds.Union(SelectedJointPartJoints()).ToHashSet();

    /// <summary>Adds the part to the selection, or removes it if it is already selected.</summary>
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

        var deepest = parts.ToList().FindLastIndex(part => SelectedSet(part.Kind).Contains(part.Id));
        foreach (var part in parts)
        {
            SelectedSet(part.Kind).Remove(part.Id);
        }

        if (deepest + 1 < parts.Count)
        {
            SelectedSet(parts[deepest + 1].Kind).Add(parts[deepest + 1].Id);
        }

        SelectionChanged();
    }

    private IEnumerable<int> SelectedPartIds() =>
        _selectedNodeIds.Concat(_selectedBeamIds).Concat(_selectedSensorIds).Concat(_selectedServoIds).Concat(_selectedPistonIds).Concat(_selectedSpringIds).Concat(_selectedWheelIds).Concat(_selectedTouchSensorIds).Concat(_selectedRubberIds).Concat(_selectedVelocityMotorIds).Concat(_selectedBrakeIds).Concat(_selectedAngleSensorIds).Concat(_selectedStepperIds);

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
        CreatureElementKind.Wheel => _selectedWheelIds,
        CreatureElementKind.TouchSensor => _selectedTouchSensorIds,
        CreatureElementKind.Rubber => _selectedRubberIds,
        CreatureElementKind.VelocityMotor => _selectedVelocityMotorIds,
        CreatureElementKind.Brake => _selectedBrakeIds,
        CreatureElementKind.AngleSensor => _selectedAngleSensorIds,
        CreatureElementKind.Stepper => _selectedStepperIds,
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
        _selectedWheelIds.Clear();
        _selectedTouchSensorIds.Clear();
        _selectedRubberIds.Clear();
        _selectedVelocityMotorIds.Clear();
        _selectedBrakeIds.Clear();
        _selectedAngleSensorIds.Clear();
        _selectedStepperIds.Clear();
    }

    private int? Single(HashSet<int> set) => SelectedPartCount == 1 && set.Count == 1 ? set.First() : null;

    // The joints under the selected joint parts, motors, Steppers, Brakes, Angle sensors, Wheels, Rubbers and Touch sensors.
    // Each joint once, though several parts of a stack can be selected.
    private HashSet<int> SelectedJointPartJoints() =>
    [
        .. _builder.Servos.Where(servo => _selectedServoIds.Contains(servo.Id)).Select(servo => servo.NodeId),
        .. _builder.VelocityMotors.Where(motor => _selectedVelocityMotorIds.Contains(motor.Id)).Select(motor => motor.NodeId),
        .. _builder.Steppers.Where(stepper => _selectedStepperIds.Contains(stepper.Id)).Select(stepper => stepper.NodeId),
        .. _builder.Brakes.Where(brake => _selectedBrakeIds.Contains(brake.Id)).Select(brake => brake.NodeId),
        .. _builder.AngleSensors.Where(angleSensor => _selectedAngleSensorIds.Contains(angleSensor.Id)).Select(angleSensor => angleSensor.NodeId),
        .. _builder.Wheels.Where(wheel => _selectedWheelIds.Contains(wheel.Id)).Select(wheel => wheel.NodeId),
        .. _builder.Rubbers.Where(rubber => _selectedRubberIds.Contains(rubber.Id)).Select(rubber => rubber.NodeId),
        .. _builder.TouchSensors.Where(touch => _selectedTouchSensorIds.Contains(touch.Id)).Select(touch => touch.NodeId),
    ];

    // Drops the ids the body no longer has.
    private void PruneSelection()
    {
        foreach (var kind in Enum.GetValues<CreatureElementKind>())
        {
            SelectedSet(kind).RemoveWhere(id => !Exists(new CreatureElementSelection(kind, id)));
        }
    }

    // A joint part stands in for its joint, also when Undo or Redo brings one back under a kept joint selection.
    private void SelectJointPartsInsteadOfTheirJoints()
    {
        foreach (var servo in _builder.Servos)
        {
            if (_selectedNodeIds.Remove(servo.NodeId))
            {
                _selectedServoIds.Add(servo.Id);
            }
        }

        foreach (var motor in _builder.VelocityMotors)
        {
            if (_selectedNodeIds.Remove(motor.NodeId))
            {
                _selectedVelocityMotorIds.Add(motor.Id);
            }
        }

        foreach (var stepper in _builder.Steppers)
        {
            if (_selectedNodeIds.Remove(stepper.NodeId))
            {
                _selectedStepperIds.Add(stepper.Id);
            }
        }

        foreach (var brake in _builder.Brakes)
        {
            if (_selectedNodeIds.Remove(brake.NodeId))
            {
                _selectedBrakeIds.Add(brake.Id);
            }
        }

        foreach (var angleSensor in _builder.AngleSensors)
        {
            if (_selectedNodeIds.Remove(angleSensor.NodeId))
            {
                _selectedAngleSensorIds.Add(angleSensor.Id);
            }
        }

        foreach (var wheel in _builder.Wheels)
        {
            if (_selectedNodeIds.Remove(wheel.NodeId))
            {
                _selectedWheelIds.Add(wheel.Id);
            }
        }

        foreach (var rubber in _builder.Rubbers)
        {
            if (_selectedNodeIds.Remove(rubber.NodeId))
            {
                _selectedRubberIds.Add(rubber.Id);
            }
        }

        foreach (var touch in _builder.TouchSensors)
        {
            if (_selectedNodeIds.Remove(touch.NodeId))
            {
                _selectedTouchSensorIds.Add(touch.Id);
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
        OnPropertyChanged(nameof(SelectedWheelCount));
        OnPropertyChanged(nameof(SingleSelectedWheelId));
        OnPropertyChanged(nameof(SelectedTouchSensorCount));
        OnPropertyChanged(nameof(SingleSelectedTouchSensorId));
        OnPropertyChanged(nameof(SelectedRubberCount));
        OnPropertyChanged(nameof(SingleSelectedRubberId));
        OnPropertyChanged(nameof(SelectedVelocityMotorCount));
        OnPropertyChanged(nameof(SingleSelectedVelocityMotorId));
        OnPropertyChanged(nameof(SelectedBrakeCount));
        OnPropertyChanged(nameof(SingleSelectedBrakeId));
        OnPropertyChanged(nameof(SelectedAngleSensorCount));
        OnPropertyChanged(nameof(SingleSelectedAngleSensorId));
        OnPropertyChanged(nameof(SelectedStepperCount));
        OnPropertyChanged(nameof(SingleSelectedStepperId));
    }
}
