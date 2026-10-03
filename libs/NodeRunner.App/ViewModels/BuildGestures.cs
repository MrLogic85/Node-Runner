using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The Select tool's handles on a selection of two or more joints, and the handle that turns a
/// selected Camera in any tool (#594).
/// </summary>
public enum SelectionHandle
{
    Move,
    Rotate,
    Scale,
    Aim,
}

/// <summary>
/// Turns pointer presses, drags and releases on the Build canvas into edits
/// for the active <see cref="BuildTool"/> and into zoom and pan of
/// <see cref="View"/>, so the canvas only forwards input and draws. Pointer
/// positions are in view units (see <see cref="CanvasView"/>); the gesture
/// state it exposes for drawing is in canvas units, the same as
/// <see cref="NodeDef.Position"/>.
/// <para>
/// The first pointer down drives the tool. A second pointer cancels that
/// gesture, putting back any node it moved, and the two pointers pinch to
/// zoom and drag to pan until every pointer is up. See
/// `docs/BUILD_MODE.md`.
/// </para>
/// </summary>
public sealed class BuildGestures
{
    /// <summary>Node hit radius in view units, so it stays finger-sized at any zoom; a node's own ring always hits too.</summary>
    public const double NodeHitRadius = 32;

    /// <summary>Beam hit distance in view units, so it stays finger-sized at any zoom.</summary>
    public const double BeamHitDistance = 20;

    /// <summary>How far a pointer may travel, in view units, and still count as a tap.</summary>
    public const double TapSlop = 8;

    /// <summary>Selection handle hit radius in view units: a 48-unit finger target at any zoom.</summary>
    public const double HandleHitRadius = 24;

    /// <summary>A selected joint's halo radius, as a multiple of its own radius; the frame clears it.</summary>
    public const double SelectedHaloScale = 1.7;

    private const double _selectionBoxMinSize = 8;
    private const double _framePadding = 8;
    private const double _frameMinSize = 96;
    private const double _rotateStem = 32;

    // The Aim handle's gap past the camera picture; no stem joins them (#622).
    private const double _aimGap = 8;

    // The owner wanted the Aim handle twice as far from the camera (#639).
    private const double _aimReachScale = 2;

    private readonly BuildViewModel _build;
    private readonly Dictionary<int, Vector2D> _pointers = [];
    private bool _navigating;
    private int? _toolPointer;
    private BuildTool _pressTool;
    private Vector2D _pressViewPosition;
    private Vector2D _pressPosition;
    private Vector2D _lastViewPosition;
    private bool _dragging;
    private int? _pressedNode;
    private int? _pressedBeam;
    private int? _pressedSensor;
    private int? _pressedPiston;
    private Vector2D? _dragOrigin;
    private (int[] Nodes, int? Beam, int? Sensor, int? Piston)? _selectionBefore;
    private SelectionHandle? _pressedHandle;
    private bool _pressedNodeWasSelected;
    private SelectionSnapshot? _selectionStart;
    private (int Sensor, double Aim)? _aimStart;

    public BuildGestures(BuildViewModel build)
    {
        _build = build ?? throw new ArgumentNullException(nameof(build));
        View = new CanvasView(BuildViewModel.BuildViewBounds, ContentBounds);
    }

    public CanvasView View { get; }

    /// <summary>Raised when the gesture's own visuals change (beam preview, selection box), so the canvas can redraw.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised just before a Move or Select drag first moves nodes; the argument is every node it moves.</summary>
    public event EventHandler<IReadOnlyCollection<int>>? NodeDragStarting;

    /// <summary>The node a Beam or Piston drag started from.</summary>
    public int? BeamStartNodeId { get; private set; }

    /// <summary>Where the pointer is during a Beam or Piston drag.</summary>
    public Vector2D? BeamEnd { get; private set; }

    /// <summary>The joint a Beam or Piston drag would connect to if released now.</summary>
    public int? BeamTargetNodeId { get; private set; }

    /// <summary>The joint under a Piston drag that would refuse it (#451), such as one a beam already joins to the start.</summary>
    public int? RefusedTargetNodeId { get; private set; }

    /// <summary>The corners of the Select tool's box while it is dragged.</summary>
    public (Vector2D Start, Vector2D End)? SelectionBox { get; private set; }

    /// <summary>
    /// The dashed frame around a Select selection of two or more joints, in
    /// canvas units: their rings plus a little padding, never smaller on
    /// screen than room for the handles.
    /// </summary>
    public CanvasRect? SelectionFrame => FrameInView() is { } frame
        ? new CanvasRect(View.ToCanvas(frame.Min), View.ToCanvas(frame.Max))
        : null;

    /// <summary>
    /// Where each handle sits, in canvas units: on <see cref="SelectionFrame"/>, Move in the
    /// middle, Rotate on a stem above and Scale at the bottom-right corner; on a selected Camera,
    /// Aim out along its aim.
    /// </summary>
    public IReadOnlyList<(SelectionHandle Handle, Vector2D Position)> SelectionHandles =>
        [.. HandlesInView().Select(entry => (entry.Handle, View.ToCanvas(entry.Position)))];

    /// <summary>
    /// The part a tray part dragged to <paramref name="viewPosition"/> would land on (#376): a
    /// joint's ring, then a sensor picture (its beam), then a beam within reach, then a joint
    /// within reach, so a drop near a joint on a short beam still reaches the beam. Null over
    /// empty canvas.
    /// </summary>
    public CreatureElementSelection? DropTargetAt(Vector2D viewPosition)
    {
        var position = View.ToCanvas(viewPosition);
        if (_build.TryFindNodeNear(position, 0, out var nodeId))
        {
            return new CreatureElementSelection(CreatureElementKind.Node, nodeId);
        }

        if (_build.TryFindSensorAt(position, out var sensorId))
        {
            var sensor = _build.Sensors.Single(entry => entry.Id == sensorId);
            return new CreatureElementSelection(CreatureElementKind.Beam, sensor.BeamId);
        }

        if (_build.TryFindBeamNear(position, HitDistance(BeamHitDistance), out var beamId))
        {
            return new CreatureElementSelection(CreatureElementKind.Beam, beamId);
        }

        return _build.TryFindNodeNear(position, HitDistance(NodeHitRadius), out nodeId)
            ? new CreatureElementSelection(CreatureElementKind.Node, nodeId)
            : null;
    }

    /// <summary>Places a tray part dropped at <paramref name="viewPosition"/>; see <see cref="BuildViewModel.PlacePart"/>.</summary>
    public int? DropPart(BuildPart part, Vector2D viewPosition) => _build.PlacePart(part, DropTargetAt(viewPosition));

    /// <summary>A pointer touches down at <paramref name="viewPosition"/>.</summary>
    public void Press(Vector2D viewPosition, int pointer = 0)
    {
        _build.DismissPlacementNote();
        _pointers[pointer] = viewPosition;
        if (_navigating)
        {
            return;
        }

        if (_pointers.Count > 1)
        {
            Cancel();
            _navigating = true;
            return;
        }

        StartToolGesture(pointer, viewPosition);
    }

    /// <summary>A pointer that is down moves to <paramref name="viewPosition"/>.</summary>
    public void Drag(Vector2D viewPosition, int pointer = 0)
    {
        if (!_pointers.TryGetValue(pointer, out var previous))
        {
            return;
        }

        _pointers[pointer] = viewPosition;
        if (_navigating)
        {
            Navigate(pointer, previous, viewPosition);
        }
        else if (pointer == _toolPointer)
        {
            DragTool(viewPosition);
        }
    }

    /// <summary>A pointer lifts at <paramref name="viewPosition"/>.</summary>
    public void Release(Vector2D viewPosition, int pointer = 0)
    {
        if (!_pointers.Remove(pointer))
        {
            return;
        }

        if (_navigating)
        {
            _navigating = _pointers.Count > 0;
        }
        else if (pointer == _toolPointer)
        {
            ReleaseTool(viewPosition);
        }
    }

    /// <summary>
    /// Undoes the tool gesture in progress: puts back any node its drag moved
    /// and the selection a Select press changed. Pointers still down stay
    /// ignored until they lift.
    /// </summary>
    public void Cancel()
    {
        if (_toolPointer is null)
        {
            return;
        }

        if (_aimStart is { } aim)
        {
            _build.SetCameraAim(aim.Sensor, aim.Aim);
        }
        else if (_selectionStart is { } start)
        {
            _build.RestoreSelection(start);
        }
        else if (_dragOrigin is { } origin && _pressedNode is { } node)
        {
            _build.MoveNode(node, origin);
        }

        if (_selectionBefore is { } before)
        {
            RestoreSelection(before.Nodes, before.Beam, before.Sensor, before.Piston);
        }

        ResetTool();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void StartToolGesture(int pointer, Vector2D viewPosition)
    {
        ResetTool();
        _toolPointer = pointer;
        _pressTool = _build.ActiveTool;
        _pressViewPosition = viewPosition;
        _lastViewPosition = viewPosition;
        var position = View.ToCanvas(viewPosition);
        _pressPosition = position;
        _pressedHandle = FindHandle(viewPosition);
        if (_pressedHandle == SelectionHandle.Aim)
        {
            // The Aim handle comes first, so nothing under it is pressed.
            return;
        }

        // Joints, then sensors, then Pistons, then beams; a joint's wider touch reach only counts off
        // its ring, so it never covers a sensor picture next to it. A Piston draws over the beams it
        // crosses, so it is hit first.
        if (_build.TryFindNodeNear(position, 0, out var nodeId))
        {
            _pressedNode = nodeId;
        }
        else if (_pressedHandle is null && _build.TryFindSensorAt(position, out var sensorId))
        {
            _pressedSensor = sensorId;
        }
        else if (_build.TryFindNodeNear(position, HitDistance(NodeHitRadius), out nodeId))
        {
            _pressedNode = nodeId;
        }
        else if (_pressedHandle is null && _build.TryFindPistonNear(position, HitDistance(BeamHitDistance), out var pistonId))
        {
            _pressedPiston = pistonId;
        }
        else if (_pressedHandle is null && _build.TryFindBeamNear(position, HitDistance(BeamHitDistance), out var beamId))
        {
            _pressedBeam = beamId;
        }

        switch (_pressTool)
        {
            case BuildTool.Beam or BuildTool.Piston when _pressedNode is { } start && !_build.IsMoveOnly:
                BeamStartNodeId = start;
                BeamEnd = position;
                Changed?.Invoke(this, EventArgs.Empty);
                break;
            case BuildTool.Select when _pressedHandle is null:
                _selectionBefore = ([.. _build.SelectedNodeIds], _build.SingleSelectedBeamId, _build.SingleSelectedSensorId, _build.SingleSelectedPistonId);
                PressSelect(viewPosition, position);
                break;
        }
    }

    private void DragTool(Vector2D viewPosition)
    {
        if (_build.ActiveTool != _pressTool)
        {
            Cancel();
            return;
        }

        if (!_dragging)
        {
            if (Distance(viewPosition, _pressViewPosition) <= TapSlop)
            {
                return;
            }

            _dragging = true;
            if (_pressedHandle == SelectionHandle.Aim)
            {
                if (_build.AimableCameraId is { } camera)
                {
                    _aimStart = (camera, _build.Sensors.Single(sensor => sensor.Id == camera).Aim ?? 0);
                }
            }
            else if (_pressTool == BuildTool.Select && (_pressedHandle is not null || _pressedNode is not null))
            {
                _selectionStart = _build.SnapshotSelection();
                NodeDragStarting?.Invoke(this, [.. _selectionStart.Positions.Keys]);
            }
            else if (_pressTool == BuildTool.Move && _pressedNode is { } dragged)
            {
                _dragOrigin = NodeById(dragged).Position;
                NodeDragStarting?.Invoke(this, [dragged]);
            }
        }

        var lastViewPosition = _lastViewPosition;
        _lastViewPosition = viewPosition;
        var position = View.ToCanvas(viewPosition);
        if (_pressedHandle == SelectionHandle.Aim)
        {
            AimCamera(position);
            return;
        }

        switch (_pressTool)
        {
            case BuildTool.Move when _pressedNode is { } node:
                _build.MoveNode(node, position);
                break;
            case BuildTool.Move:
                View.PanBy(new Vector2D(viewPosition.X - lastViewPosition.X, viewPosition.Y - lastViewPosition.Y));
                break;
            case BuildTool.Beam when BeamStartNodeId is { } start:
                BeamEnd = position;
                BeamTargetNodeId = FindBeamTarget(start, position);
                Changed?.Invoke(this, EventArgs.Empty);
                break;
            case BuildTool.Piston when BeamStartNodeId is { } start:
                BeamEnd = position;
                var target = FindPistonTarget(start, position);
                var refused = target is { } end && !_build.CanConnectPiston(start, end, out _);
                BeamTargetNodeId = refused ? null : target;
                RefusedTargetNodeId = refused ? target : null;
                Changed?.Invoke(this, EventArgs.Empty);
                break;
            case BuildTool.Select when _selectionStart is { } start:
                TransformSelection(start, position);
                break;
            case BuildTool.Select when SelectionBox is { } box:
                SelectionBox = (box.Start, position);
                Changed?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private void ReleaseTool(Vector2D viewPosition)
    {
        if (_build.ActiveTool != _pressTool)
        {
            Cancel();
            return;
        }

        var position = View.ToCanvas(viewPosition);
        switch (_pressTool)
        {
            case BuildTool when _pressedHandle == SelectionHandle.Aim:
                break;
            case BuildTool.Move when !_dragging:
                TapMove();
                break;
            case BuildTool.Beam when BeamStartNodeId is { } start && FindBeamTarget(start, position) is { } end:
                _build.ConnectBeam(start, end);
                break;
            case BuildTool.Piston when BeamStartNodeId is { } start && _dragging:
                if (FindPistonTarget(start, position) is { } pistonEnd)
                {
                    _build.ConnectPiston(start, pistonEnd);
                }
                else
                {
                    _build.PistonDropMissed();
                }

                break;
            case BuildTool.Joint when !_dragging:
                TapJoint();
                break;
            case BuildTool.Select when SelectionBox is { } box:
                CompleteSelectionBox(box.Start, position);
                break;
            case BuildTool.Select when !_dragging && (_pressedNodeWasSelected || _pressedHandle is not null) && _pressedNode is { } tapped:
                // A handle drags; a tap on a joint under it still adds or removes that joint.
                _build.ToggleSelectedNode(tapped);
                break;
        }

        ResetTool();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RestoreSelection(int[] nodes, int? beam, int? sensor, int? piston)
    {
        if (piston is { } selectedPiston)
        {
            if (_build.SingleSelectedPistonId != selectedPiston)
            {
                _build.SelectPiston(selectedPiston);
            }
        }
        else if (sensor is { } selectedSensor)
        {
            if (_build.SingleSelectedSensorId != selectedSensor)
            {
                _build.SelectSensor(selectedSensor);
            }
        }
        else if (beam is { } selectedBeam)
        {
            if (_build.SingleSelectedBeamId != selectedBeam)
            {
                _build.SelectBeam(selectedBeam);
            }
        }
        else if (_build.SelectedBeamCount != 0
            || _build.SelectedSensorCount != 0
            || _build.SelectedPistonCount != 0
            || _build.SelectedNodeIds.Count != nodes.Length
            || !nodes.All(_build.SelectedNodeIds.Contains))
        {
            _build.ReplaceSelection(nodes);
        }
    }

    private void Navigate(int pointer, Vector2D previous, Vector2D current)
    {
        if (_pointers.Count != 2)
        {
            return;
        }

        var other = _pointers.First(entry => entry.Key != pointer).Value;
        var previousMid = Midpoint(previous, other);
        var currentMid = Midpoint(current, other);
        View.PanBy(new Vector2D(currentMid.X - previousMid.X, currentMid.Y - previousMid.Y));
        var previousSpan = Distance(previous, other);
        if (previousSpan > 0)
        {
            View.ZoomAbout(currentMid, Distance(current, other) / previousSpan);
        }
    }

    /// <summary>
    /// A press on a joint adds it (a tap on one already selected removes it
    /// on release); a press inside the frame drags the selection; anywhere
    /// else, sensors and beams included, clears the selection and starts a box.
    /// </summary>
    private void PressSelect(Vector2D viewPosition, Vector2D position)
    {
        if (_pressedNode is { } node)
        {
            _pressedNodeWasSelected = _build.SelectedNodeIds.Contains(node);
            if (!_pressedNodeWasSelected)
            {
                _build.ToggleSelectedNode(node);
            }
        }
        else if (FrameInView() is { } frame && frame.Contains(viewPosition))
        {
            _pressedHandle = SelectionHandle.Move;
        }
        else
        {
            _build.ClearSelection();
            SelectionBox = (position, position);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void TapMove()
    {
        if (_pressedNode is { } node)
        {
            _build.ReplaceSelection([node]);
        }
        else if (_pressedSensor is { } sensor)
        {
            _build.SelectSensor(sensor);
        }
        else if (_pressedPiston is { } piston)
        {
            _build.SelectPiston(piston);
        }
        else if (_pressedBeam is { } beam)
        {
            _build.SelectBeam(beam);
        }
        else
        {
            _build.ClearSelection();
        }
    }

    private void TapJoint()
    {
        if (_pressedNode is not null || _pressedSensor is not null || _pressedPiston is not null || _build.IsMoveOnly)
        {
            return;
        }

        if (_pressedBeam is { } beam)
        {
            _build.SplitBeam(beam, _pressPosition);
        }
        else if (BuildViewModel.BuildArea.Contains(_pressPosition))
        {
            _build.PlaceNode(_pressPosition);
        }
    }

    /// <summary>Turns the Camera the Aim drag started on to look at <paramref name="position"/>, smoothly (#622).</summary>
    private void AimCamera(Vector2D position)
    {
        if (_aimStart is not { } start)
        {
            return;
        }

        var (nodeA, nodeB) = CameraBeam(start.Sensor);
        var middle = Midpoint(nodeA, nodeB);
        if (position == middle)
        {
            return;
        }

        _build.SetCameraAim(start.Sensor, CameraRays.AimAlong(Math.Atan2(position.Y - middle.Y, position.X - middle.X), nodeA, nodeB));
    }

    private (Vector2D NodeA, Vector2D NodeB) CameraBeam(int sensorId)
    {
        var sensor = _build.Sensors.Single(entry => entry.Id == sensorId);
        var beam = _build.Beams[_build.BeamIndexOf(sensor.BeamId)];
        return (NodeById(beam.NodeA).Position, NodeById(beam.NodeB).Position);
    }

    /// <summary>Applies a Select drag to <paramref name="position"/>, always measured from the press and the start snapshot.</summary>
    private void TransformSelection(SelectionSnapshot start, Vector2D position)
    {
        var pivot = start.Pivot;
        var from = new Vector2D(_pressPosition.X - pivot.X, _pressPosition.Y - pivot.Y);
        var to = new Vector2D(position.X - pivot.X, position.Y - pivot.Y);
        switch (_pressedHandle)
        {
            case SelectionHandle.Rotate:
                _build.RotateSelection(start, Math.Atan2(to.Y, to.X) - Math.Atan2(from.Y, from.X));
                break;
            case SelectionHandle.Scale when (from.X * from.X) + (from.Y * from.Y) > 0:
                // Only the drag along the handle's diagonal counts, so the group never reflects.
                _build.ScaleSelection(start, ((to.X * from.X) + (to.Y * from.Y)) / ((from.X * from.X) + (from.Y * from.Y)));
                break;
            case SelectionHandle.Scale:
                break;
            default:
                _build.TranslateSelection(start, new Vector2D(position.X - _pressPosition.X, position.Y - _pressPosition.Y));
                break;
        }
    }

    /// <summary>The frame in view units, while Select has two or more joints and no box is being dragged.</summary>
    private CanvasRect? FrameInView()
    {
        if (_build.ActiveTool != BuildTool.Select || _build.SelectedNodeCount < 2 || SelectionBox is not null)
        {
            return null;
        }

        var nodes = _build.SelectedNodeIds.Select(NodeById).ToArray();
        var min = View.ToView(new Vector2D(nodes.Min(node => node.Position.X - Halo(node)), nodes.Min(node => node.Position.Y - Halo(node))));
        var max = View.ToView(new Vector2D(nodes.Max(node => node.Position.X + Halo(node)), nodes.Max(node => node.Position.Y + Halo(node))));
        var center = new Vector2D((min.X + max.X) / 2, (min.Y + max.Y) / 2);
        var halfWidth = Math.Max((max.X - min.X) / 2 + _framePadding, _frameMinSize / 2);
        var halfHeight = Math.Max((max.Y - min.Y) / 2 + _framePadding, _frameMinSize / 2);
        return new CanvasRect(
            new Vector2D(center.X - halfWidth, center.Y - halfHeight),
            new Vector2D(center.X + halfWidth, center.Y + halfHeight));
    }

    private static double Halo(NodeDef node) => node.Radius * SelectedHaloScale;

    private IEnumerable<(SelectionHandle Handle, Vector2D Position)> HandlesInView()
    {
        if (_build.AimableCameraId is { } camera)
        {
            yield return (SelectionHandle.Aim, AimHandleInView(camera));
        }

        if (FrameInView() is not { } frame)
        {
            yield break;
        }

        yield return (SelectionHandle.Move, frame.Center);
        yield return (SelectionHandle.Rotate, new Vector2D(frame.Center.X, frame.Min.Y - _rotateStem));
        yield return (SelectionHandle.Scale, frame.Max);
    }

    /// <summary>
    /// The Aim handle out along the camera's centre ray, just past its picture, so it follows the
    /// zoom smoothly (#639). It may cover a joint; it only shows while the camera is selected.
    /// </summary>
    private Vector2D AimHandleInView(int camera)
    {
        var (nodeA, nodeB) = CameraBeam(camera);
        var aim = CameraRays.BeamAngle(nodeA, nodeB) + (_build.Sensors.Single(sensor => sensor.Id == camera).Aim ?? 0);
        var middle = View.ToView(Midpoint(nodeA, nodeB));
        var direction = new Vector2D(Math.Cos(aim), Math.Sin(aim));
        var reach = _aimReachScale * ((SensorPicture.CameraSize / Math.Sqrt(2) * View.Zoom) + _aimGap + HandleHitRadius);
        return new Vector2D(middle.X + (direction.X * reach), middle.Y + (direction.Y * reach));
    }

    private SelectionHandle? FindHandle(Vector2D viewPosition) =>
        HandlesInView()
            .Where(entry => Distance(entry.Position, viewPosition) <= HandleHitRadius)
            .OrderBy(entry => Distance(entry.Position, viewPosition))
            .Select(entry => (SelectionHandle?)entry.Handle)
            .FirstOrDefault();

    /// <summary>Snaps only to joints the beam could actually join, so the preview never promises a refused connection.</summary>
    private int? FindBeamTarget(int start, Vector2D position) =>
        _build.TryFindNodeNear(position, HitDistance(NodeHitRadius), out var end) && _build.CanConnect(start, end) ? end : null;

    /// <summary>Any other joint under the pointer, so a refused Piston drop can say why there.</summary>
    private int? FindPistonTarget(int start, Vector2D position) =>
        _build.TryFindNodeNear(position, HitDistance(NodeHitRadius), out var end) && end != start ? end : null;

    private void CompleteSelectionBox(Vector2D start, Vector2D end)
    {
        var minX = Math.Min(start.X, end.X);
        var maxX = Math.Max(start.X, end.X);
        var minY = Math.Min(start.Y, end.Y);
        var maxY = Math.Max(start.Y, end.Y);
        if (maxX - minX < _selectionBoxMinSize && maxY - minY < _selectionBoxMinSize)
        {
            return;
        }

        var selected = new List<int>();
        for (var i = 0; i < _build.Nodes.Count; i++)
        {
            var node = _build.Nodes[i].Position;
            if (node.X >= minX && node.X <= maxX && node.Y >= minY && node.Y <= maxY)
            {
                selected.Add(_build.Nodes[i].Id);
            }
        }

        _build.ReplaceSelection(selected);
    }

    private double HitDistance(double viewDistance) => viewDistance / View.Zoom;

    /// <summary>What the view fits on open: every node with a margin of twice its radius, so selection rings stay in view.</summary>
    private CanvasRect? ContentBounds()
    {
        var nodes = _build.Nodes;
        if (nodes.Count == 0)
        {
            return null;
        }

        return new CanvasRect(
            new Vector2D(nodes.Min(node => node.Position.X - (2 * node.Radius)), nodes.Min(node => node.Position.Y - (2 * node.Radius))),
            new Vector2D(nodes.Max(node => node.Position.X + (2 * node.Radius)), nodes.Max(node => node.Position.Y + (2 * node.Radius))));
    }

    private void ResetTool()
    {
        _toolPointer = null;
        _dragging = false;
        _dragOrigin = null;
        _selectionBefore = null;
        _pressedHandle = null;
        _pressedNodeWasSelected = false;
        _selectionStart = null;
        _aimStart = null;
        _pressedNode = null;
        _pressedBeam = null;
        _pressedSensor = null;
        _pressedPiston = null;
        BeamStartNodeId = null;
        BeamEnd = null;
        BeamTargetNodeId = null;
        RefusedTargetNodeId = null;
        SelectionBox = null;
    }

    private NodeDef NodeById(int nodeId) => _build.Nodes[_build.NodeIndexOf(nodeId)];

    private static Vector2D Midpoint(Vector2D a, Vector2D b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);

    private static double Distance(Vector2D a, Vector2D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
