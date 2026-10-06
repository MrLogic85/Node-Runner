using NodeRunner.App.Builders;
using NodeRunner.Domain;
using NodeRunner.Mechanics;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The handles on a selection of two or more joints in any tool (#803), and the handle that turns a
/// selected Camera (#594).
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
    /// <summary>Beam hit distance in view units, so it stays finger-sized at any zoom.</summary>
    public const double BeamHitDistance = 20;

    /// <summary>How far a pointer may travel, in view units, and still count as a tap.</summary>
    public const double TapSlop = 8;

    /// <summary>Selection handle hit radius in view units: a 48-unit finger target at any zoom.</summary>
    public const double HandleHitRadius = 24;

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
    private CreatureElementSelection? _pressedLink;
    private PartSet? _selectionBefore;
    private SelectionHandle? _pressedHandle;
    private bool _pressedNodeWasSelected;
    private SharedPress _press;
    private SelectionSnapshot? _selectionStart;
    private FrameLayout? _dragLayout;
    private double _turn;
    private double _frameAngle;
    private (int Sensor, double Aim)? _aimStart;

    public BuildGestures(BuildViewModel build)
    {
        _build = build ?? throw new ArgumentNullException(nameof(build));
        View = new CanvasView(BuildViewModel.BuildViewBounds, ContentBounds);
        _build.PropertyChanged += (_, args) =>
        {
            // A new selection gets an upright frame.
            if (args.PropertyName == nameof(BuildViewModel.SelectedNodeCount))
            {
                _frameAngle = 0;
            }
        };
    }

    public CanvasView View { get; }

    /// <summary>Raised when the gesture's own visuals change (beam preview, selection box), so the canvas can redraw.</summary>
    public event EventHandler? Changed;

    /// <summary>The node a Beam or link drag started from.</summary>
    public int? BeamStartNodeId { get; private set; }

    /// <summary>Where the pointer is during a Beam or link drag.</summary>
    public Vector2D? BeamEnd { get; private set; }

    /// <summary>The joint a Beam or link drag would connect to if released now.</summary>
    public int? BeamTargetNodeId { get; private set; }

    /// <summary>The joint under a link drag that would refuse it (#451, #877), such as one a beam already joins to the start.</summary>
    public int? RefusedTargetNodeId { get; private set; }

    /// <summary>The corners of the Select tool's box while it is dragged.</summary>
    public (Vector2D Start, Vector2D End)? SelectionBox { get; private set; }

    /// <summary>
    /// The frame around two or more selected joints, padded and never smaller on screen than room for
    /// the handles, in canvas units, before <see cref="SelectionFrameAngle"/> turns it.
    /// </summary>
    public CanvasRect? SelectionFrame => Layout() is { } layout
        ? new CanvasRect(View.ToCanvas(layout.Frame.Min), View.ToCanvas(layout.Frame.Max))
        : null;

    /// <summary>
    /// How far <see cref="SelectionFrame"/> is turned, in radians; kept from the last Rotate until
    /// the selection changes.
    /// </summary>
    public double SelectionFrameAngle => Layout()?.Angle ?? 0;

    /// <summary>
    /// The corners of <see cref="SelectionFrame"/> that show a corner square: all but the Scale
    /// handle's bottom-right, in canvas units, before <see cref="SelectionFrameAngle"/> turns them.
    /// </summary>
    public IReadOnlyList<Vector2D> FrameCornerSquares => Layout() is { Frame: var frame }
        ? [View.ToCanvas(frame.Min), View.ToCanvas(new Vector2D(frame.Max.X, frame.Min.Y)), View.ToCanvas(new Vector2D(frame.Min.X, frame.Max.Y))]
        : [];

    /// <summary>The line from the frame's top edge to the Rotate handle, in canvas units.</summary>
    public (Vector2D From, Vector2D To)? RotateStem => Layout() is { Stem: var stem }
        ? (View.ToCanvas(stem.From), View.ToCanvas(stem.To))
        : null;

    /// <summary>
    /// Where each handle sits, in canvas units: on <see cref="SelectionFrame"/>, Move in the
    /// middle, Rotate on a stem above and Scale at the bottom-right corner, all turned with the
    /// frame; on a selected Camera, Aim out along its aim.
    /// </summary>
    public IReadOnlyList<(SelectionHandle Handle, Vector2D Position)> SelectionHandles =>
        [.. HandlesInView().Select(entry => (entry.Handle, View.ToCanvas(entry.Position)))];

    /// <summary>The parts the Select box would select if it were released now; none while no box is dragged.</summary>
    public PartSet SelectionBoxCatches =>
        SelectionBox is { } box ? PartsInBox(box.Start, box.End) ?? PartSet.None : PartSet.None;

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

        return _build.TryFindNodeNear(position, SelectionMarks.Gap, out nodeId)
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
    /// and the selection its press changed. Pointers still down stay
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
            _build.SetParameter(PartParameterId.Aim, aim.Aim);
        }
        else if (_selectionStart is { } start)
        {
            _build.RestoreSelection(start);
        }

        if (_selectionBefore is { } before)
        {
            RestoreSelection(before);
        }

        _build.CancelEdit(this);
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

        // Joints, then sensors, then links, then beams; a joint's wider touch reach only counts off
        // its ring, so it never covers a sensor picture next to it. A Piston or Spring draws over the
        // beams it crosses, so it is hit first.
        if (_build.TryFindNodeNear(position, 0, out var nodeId))
        {
            _pressedNode = nodeId;
        }
        else if (_build.TryFindSensorAt(position, out var sensorId))
        {
            _pressedSensor = sensorId;
        }
        else if (_build.TryFindNodeNear(position, SelectionMarks.Gap, out nodeId))
        {
            _pressedNode = nodeId;
        }
        else if (_build.TryFindLinkNear(position, HitDistance(BeamHitDistance), out var link))
        {
            _pressedLink = link;
        }
        else if (_build.TryFindBeamNear(position, HitDistance(BeamHitDistance), out var beamId))
        {
            _pressedBeam = beamId;
        }

        _selectionBefore = _build.Selection;
        _pressedNodeWasSelected = _pressedNode is { } node && _build.SelectedNodeIds.Contains(node);
        if (_pressedHandle is not null)
        {
            return;
        }

        if (_pressTool == BuildTool.Beam && _pressedNode is { } start && !_build.IsMoveOnly && !_pressedNodeWasSelected)
        {
            BeamStartNodeId = start;
            BeamEnd = position;
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        PressShared(viewPosition);
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

            // Everything a drag changes, up to its release, is one undo step (#689).
            _build.BeginEdit(this);
            if (_pressedHandle == SelectionHandle.Aim)
            {
                if (_build.AimableCameraId is { } camera)
                {
                    _aimStart = (camera, _build.Sensors.Single(sensor => sensor.Id == camera).Aim ?? 0);
                }
            }
            else if (_pressedHandle is not null || _press is SharedPress.Move or SharedPress.Group)
            {
                if (_press == SharedPress.Move && !_pressedNodeWasSelected && _pressedNode is { } joint)
                {
                    _build.ReplaceSelection([joint]);
                }

                // The group turns and scales about the frame's centre, where the Move handle is.
                _dragLayout = Layout();
                _selectionStart = _build.SnapshotSelection(_dragLayout is { } layout ? View.ToCanvas(layout.Move) : null);
            }
            else if (_press == SharedPress.Box)
            {
                _build.ClearSelection();
                SelectionBox = (_pressPosition, _pressPosition);
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

        if (BeamStartNodeId is { } start)
        {
            BeamEnd = position;
            var target = FindLinkTarget(start, position);
            var refused = target is { } end && !_build.CanConnectLink(_build.PickedLink, start, end, out _);
            BeamTargetNodeId = refused ? null : target;
            RefusedTargetNodeId = refused ? target : null;

            Changed?.Invoke(this, EventArgs.Empty);
        }
        else if (_selectionStart is { } snapshot)
        {
            TransformSelection(snapshot, position);
        }
        else if (SelectionBox is { } box)
        {
            SelectionBox = (box.Start, position);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        else if (_press == SharedPress.Pan)
        {
            View.PanBy(new Vector2D(viewPosition.X - lastViewPosition.X, viewPosition.Y - lastViewPosition.Y));
        }
    }

    private void ReleaseTool(Vector2D viewPosition)
    {
        if (_build.ActiveTool != _pressTool)
        {
            Cancel();
            return;
        }

        // A tap is an edit of its own too, so it never folds into a slider edit another finger holds open.
        _build.BeginEdit(this);
        var position = View.ToCanvas(viewPosition);
        if (!_dragging)
        {
            Tap();
        }
        else if (BeamStartNodeId is { } start)
        {
            // A link never makes a joint, so a drop away from one places nothing.
            if (FindLinkTarget(start, position) is { } end)
            {
                _build.ConnectLink(_build.PickedLink, start, end);
            }
        }
        else if (SelectionBox is { } box)
        {
            CompleteSelectionBox(box.Start, position);
        }
        else if (_pressedHandle == SelectionHandle.Rotate && _dragLayout is { } turned)
        {
            _frameAngle = Math.IEEERemainder(turned.Angle + _turn, 2 * Math.PI);
        }

        _build.EndEdit(this);
        ResetTool();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RestoreSelection(PartSet before)
    {
        var now = _build.Selection;
        if (!Enum.GetValues<CreatureElementKind>().All(kind => now.SetOf(kind).SetEquals(before.SetOf(kind))))
        {
            _build.ReplaceSelection(before);
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
    /// What a press does once it drags, the same in every tool (#803) apart from a Beams link drag:
    /// a selected joint drags the selection, a press inside a group's frame drags the group, an
    /// unselected joint is moved alone, and any other press draws a box in Select and pans elsewhere.
    /// Taps are settled on release (<see cref="Tap"/>).
    /// </summary>
    private void PressShared(Vector2D viewPosition)
    {
        if (_pressedNodeWasSelected)
        {
            _press = SharedPress.Move;
        }
        else if (_build.SelectedNodeCount >= 2 && Layout() is { } layout && FrameContains(layout, viewPosition))
        {
            _press = SharedPress.Group;
        }
        else if (_pressedNode is not null)
        {
            _press = SharedPress.Move;
        }
        else
        {
            _press = _pressTool == BuildTool.Select ? SharedPress.Box : SharedPress.Pan;
        }
    }

    /// <summary>
    /// A tap in any tool (#803) adds or removes the part under it. A tap on empty canvas clears the
    /// selection, or with nothing selected adds a joint in Joint; one on a handle does nothing.
    /// </summary>
    private void Tap()
    {
        if (PressedElement() is { } element)
        {
            _build.ToggleSelected(element);
        }
        else if (_pressedHandle is not null)
        {
            return;
        }
        else if (_pressTool == BuildTool.Joint && _build.Selection.Count == 0)
        {
            if (!_build.IsMoveOnly && BuildViewModel.BuildArea.Contains(_pressPosition))
            {
                _build.PlaceNode(_pressPosition);
            }
        }
        else
        {
            _build.ClearSelection();
        }
    }

    private CreatureElementSelection? PressedElement() =>
        _pressedNode is { } node ? (_build.ServoAtNode(node) is { } servo ? new(CreatureElementKind.Servo, servo) : new(CreatureElementKind.Node, node))
        : _pressedSensor is { } sensor ? new(CreatureElementKind.Sensor, sensor)
        : _pressedLink is { } link ? link
        : _pressedBeam is { } beam ? new(CreatureElementKind.Beam, beam)
        : null;

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

        _build.SetParameter(PartParameterId.Aim, CameraRays.AimAlong(Math.Atan2(position.Y - middle.Y, position.X - middle.X), nodeA, nodeB));
    }

    private (Vector2D NodeA, Vector2D NodeB) CameraBeam(int sensorId)
    {
        var sensor = _build.Sensors.Single(entry => entry.Id == sensorId);
        var beam = _build.Beams[_build.BeamIndexOf(sensor.BeamId)];
        return (NodeById(beam.NodeA).Position, NodeById(beam.NodeB).Position);
    }

    /// <summary>Applies a selection drag to <paramref name="position"/>, always measured from the press and the start snapshot.</summary>
    private void TransformSelection(SelectionSnapshot start, Vector2D position)
    {
        var pivot = start.Pivot;
        var from = new Vector2D(_pressPosition.X - pivot.X, _pressPosition.Y - pivot.Y);
        var to = new Vector2D(position.X - pivot.X, position.Y - pivot.Y);
        switch (_pressedHandle)
        {
            case SelectionHandle.Rotate:
                var turn = Math.Atan2(to.Y, to.X) - Math.Atan2(from.Y, from.X);
                if (_build.RotateSelection(start, turn))
                {
                    _turn = turn;
                }

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

    /// <summary>
    /// The frame and handles in view units while two or more joints are selected and no box is
    /// dragged: fitted to the joints along its own axes, or during a Rotate, the start layout turned.
    /// </summary>
    private FrameLayout? Layout()
    {
        if (_dragLayout is { } started && _pressedHandle == SelectionHandle.Rotate)
        {
            return Place(started.Frame, started.Angle + _turn);
        }

        if (_build.SelectedNodeCount < 2 || SelectionBox is not null)
        {
            return null;
        }

        var cos = Math.Cos(_frameAngle);
        var sin = Math.Sin(_frameAngle);
        var (minX, minY, maxX, maxY) = (double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
        foreach (var node in _build.SelectedNodeIds.Select(NodeById))
        {
            // The joint's centre along the frame's axes; its ring reaches the halo every way.
            var at = View.ToView(node.Position);
            var x = (at.X * cos) + (at.Y * sin);
            var y = (at.Y * cos) - (at.X * sin);
            var halo = SelectionMarks.JointHalo(_build.NodeRadius(node.Id)) * View.Zoom;
            (minX, minY, maxX, maxY) = (Math.Min(minX, x - halo), Math.Min(minY, y - halo), Math.Max(maxX, x + halo), Math.Max(maxY, y + halo));
        }

        var (middleX, middleY) = ((minX + maxX) / 2, (minY + maxY) / 2);
        var center = new Vector2D((middleX * cos) - (middleY * sin), (middleX * sin) + (middleY * cos));
        var halfWidth = Math.Max(((maxX - minX) / 2) + _framePadding, _frameMinSize / 2);
        var halfHeight = Math.Max(((maxY - minY) / 2) + _framePadding, _frameMinSize / 2);
        var frame = new CanvasRect(
            new Vector2D(center.X - halfWidth, center.Y - halfHeight),
            new Vector2D(center.X + halfWidth, center.Y + halfHeight));
        return Place(frame, _frameAngle);
    }

    /// <summary>The handles on <paramref name="frame"/> turned <paramref name="angle"/> about its centre.</summary>
    private static FrameLayout Place(CanvasRect frame, double angle)
    {
        var center = frame.Center;
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        Vector2D At(double x, double y) => new(center.X + (x * cos) - (y * sin), center.Y + (x * sin) + (y * cos));

        var rotate = At(0, -(frame.Height / 2) - _rotateStem);
        return new FrameLayout(frame, angle, center, rotate, At(frame.Width / 2, frame.Height / 2), (At(0, -frame.Height / 2), rotate));
    }

    /// <summary>Whether <paramref name="point"/> is inside the layout's frame, turned by its angle.</summary>
    private static bool FrameContains(FrameLayout layout, Vector2D point)
    {
        var dx = point.X - layout.Move.X;
        var dy = point.Y - layout.Move.Y;
        var cos = Math.Cos(layout.Angle);
        var sin = Math.Sin(layout.Angle);
        return Math.Abs((dx * cos) + (dy * sin)) <= layout.Frame.Width / 2
            && Math.Abs((dy * cos) - (dx * sin)) <= layout.Frame.Height / 2;
    }

    private IEnumerable<(SelectionHandle Handle, Vector2D Position)> HandlesInView()
    {
        if (_build.AimableCameraId is { } camera)
        {
            yield return (SelectionHandle.Aim, AimHandleInView(camera));
        }

        if (Layout() is not { } layout)
        {
            yield break;
        }

        yield return (SelectionHandle.Move, layout.Move);
        yield return (SelectionHandle.Rotate, layout.Rotate);
        yield return (SelectionHandle.Scale, layout.Scale);
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

    /// <summary>Any other joint under the pointer, so a refused link drop can say why there.</summary>
    private int? FindLinkTarget(int start, Vector2D position) =>
        _build.TryFindNodeNear(position, SelectionMarks.Gap, out var end) && end != start ? end : null;

    private void CompleteSelectionBox(Vector2D start, Vector2D end)
    {
        if (PartsInBox(start, end) is { } selected)
        {
            _build.ReplaceSelection(selected);
        }
    }

    /// <summary>
    /// The parts whose centres lie in the box from <paramref name="start"/> to <paramref name="end"/>
    /// (#704): a joint's centre, a beam's or link's midpoint, and a sensor's, which is its beam's
    /// midpoint. Null while the box is too small to count.
    /// </summary>
    private PartSet? PartsInBox(Vector2D start, Vector2D end)
    {
        var min = new Vector2D(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y));
        var max = new Vector2D(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y));
        if (max.X - min.X < _selectionBoxMinSize && max.Y - min.Y < _selectionBoxMinSize)
        {
            return null;
        }

        bool Inside(Vector2D point) => point.X >= min.X && point.X <= max.X && point.Y >= min.Y && point.Y <= max.Y;
        bool MidInside(int nodeA, int nodeB) => Inside(Midpoint(NodeById(nodeA).Position, NodeById(nodeB).Position));
        var beams = _build.Beams.Where(beam => MidInside(beam.NodeA, beam.NodeB)).Select(beam => beam.Id).ToHashSet();
        return new PartSet(
            _build.Nodes.Where(node => Inside(node.Position)).Select(node => node.Id).ToHashSet(),
            beams,
            _build.Sensors.Where(sensor => beams.Contains(sensor.BeamId)).Select(sensor => sensor.Id).ToHashSet(),
            _build.Servos.Where(servo => Inside(NodeById(servo.NodeId).Position)).Select(servo => servo.Id).ToHashSet(),
            _build.Pistons.Where(piston => MidInside(piston.NodeA, piston.NodeB)).Select(piston => piston.Id).ToHashSet(),
            _build.Springs.Where(spring => MidInside(spring.NodeA, spring.NodeB)).Select(spring => spring.Id).ToHashSet());
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
            new Vector2D(nodes.Min(node => node.Position.X - (2 * _build.NodeRadius(node.Id))), nodes.Min(node => node.Position.Y - (2 * _build.NodeRadius(node.Id)))),
            new Vector2D(nodes.Max(node => node.Position.X + (2 * _build.NodeRadius(node.Id))), nodes.Max(node => node.Position.Y + (2 * _build.NodeRadius(node.Id)))));
    }

    private void ResetTool()
    {
        _toolPointer = null;
        _dragging = false;
        _selectionBefore = null;
        _pressedHandle = null;
        _pressedNodeWasSelected = false;
        _press = SharedPress.None;
        _selectionStart = null;
        _dragLayout = null;
        _turn = 0;
        _aimStart = null;
        _pressedNode = null;
        _pressedBeam = null;
        _pressedSensor = null;
        _pressedLink = null;
        BeamStartNodeId = null;
        BeamEnd = null;
        BeamTargetNodeId = null;
        RefusedTargetNodeId = null;
        SelectionBox = null;
    }

    private NodeDef NodeById(int nodeId) => _build.Nodes[_build.NodeIndexOf(nodeId)];

    /// <summary>The selection frame and handles in view units; <see cref="Angle"/> turns <see cref="Frame"/> about its centre.</summary>
    private sealed record FrameLayout(
        CanvasRect Frame,
        double Angle,
        Vector2D Move,
        Vector2D Rotate,
        Vector2D Scale,
        (Vector2D From, Vector2D To) Stem);

    private static Vector2D Midpoint(Vector2D a, Vector2D b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);

    private static double Distance(Vector2D a, Vector2D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>What a press away from the handles does once it drags (#803).</summary>
    private enum SharedPress
    {
        None,
        Move,

        /// <summary>Inside a group's frame: a drag moves the group, a tap acts as if outside it.</summary>
        Group,
        Pan,
        Box,
    }
}
