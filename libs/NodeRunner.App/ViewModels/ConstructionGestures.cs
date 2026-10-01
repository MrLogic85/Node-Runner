using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>The Select tool's handles on a selection of two or more joints.</summary>
public enum SelectionHandle
{
    Move,
    Rotate,
    Scale,
}

/// <summary>
/// Turns pointer presses, drags and releases on the Build canvas into edits
/// for the active <see cref="ConstructionTool"/> and into zoom and pan of
/// <see cref="View"/>, so the canvas only forwards input and draws. Pointer
/// positions are in view units (see <see cref="CanvasView"/>); the gesture
/// state it exposes for drawing is in canvas units, the same as
/// <see cref="NodeDef.Position"/>.
/// <para>
/// The first pointer down drives the tool. A second pointer cancels that
/// gesture, putting back any node it moved, and the two pointers pinch to
/// zoom and drag to pan until every pointer is up. See
/// `docs/CONSTRUCTION_MODE.md`.
/// </para>
/// </summary>
public sealed class ConstructionGestures
{
    /// <summary>Node hit radius in view units, so it stays finger-sized at any zoom; a node's own disc always hits too.</summary>
    public const double NodeHitRadius = 32;

    /// <summary>Beam hit distance in view units, so it stays finger-sized at any zoom.</summary>
    public const double BeamHitDistance = 20;

    public const double NewNodeRadius = 18;

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

    private readonly ConstructionViewModel _construction;
    private readonly Dictionary<int, Vector2D> _pointers = [];
    private bool _navigating;
    private int? _toolPointer;
    private ConstructionTool _pressTool;
    private Vector2D _pressViewPosition;
    private Vector2D _pressPosition;
    private Vector2D _lastViewPosition;
    private bool _dragging;
    private int? _pressedNode;
    private int? _pressedBeam;
    private Vector2D? _dragOrigin;
    private (int[] Nodes, int? Beam)? _selectionBefore;
    private SelectionHandle? _pressedHandle;
    private bool _pressedNodeWasSelected;
    private SelectionSnapshot? _selectionStart;

    public ConstructionGestures(ConstructionViewModel construction)
    {
        _construction = construction ?? throw new ArgumentNullException(nameof(construction));
        View = new CanvasView(ConstructionViewModel.BuildViewBounds, ContentBounds);
    }

    public CanvasView View { get; }

    /// <summary>Raised when the gesture's own visuals change (beam preview, selection box), so the canvas can redraw.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised just before a Move or Select drag first moves nodes; the argument is every node it moves.</summary>
    public event EventHandler<IReadOnlyCollection<int>>? NodeDragStarting;

    /// <summary>The node a Beam drag started from.</summary>
    public int? BeamStartNode { get; private set; }

    /// <summary>Where the pointer is during a Beam drag.</summary>
    public Vector2D? BeamEnd { get; private set; }

    /// <summary>The joint a Beam drag would connect to if released now.</summary>
    public int? BeamTargetNode { get; private set; }

    /// <summary>The corners of the Select tool's box while it is dragged.</summary>
    public (Vector2D Start, Vector2D End)? SelectionBox { get; private set; }

    /// <summary>
    /// The dashed frame around a Select selection of two or more joints, in
    /// canvas units: their discs plus a little padding, never smaller on
    /// screen than room for the handles.
    /// </summary>
    public CanvasRect? SelectionFrame => FrameInView() is { } frame
        ? new CanvasRect(View.ToCanvas(frame.Min), View.ToCanvas(frame.Max))
        : null;

    /// <summary>
    /// Where each handle on <see cref="SelectionFrame"/> sits, in canvas
    /// units: Move in the middle, Rotate on a stem above, Scale at the
    /// bottom-right corner.
    /// </summary>
    public IReadOnlyList<(SelectionHandle Handle, Vector2D Position)> SelectionHandles =>
        [.. HandlesInView().Select(entry => (entry.Handle, View.ToCanvas(entry.Position)))];

    /// <summary>A pointer touches down at <paramref name="viewPosition"/>.</summary>
    public void Press(Vector2D viewPosition, int pointer = 0)
    {
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

        if (_selectionStart is { } start)
        {
            _construction.RestoreSelection(start);
        }
        else if (_dragOrigin is { } origin && _pressedNode is { } node)
        {
            _construction.MoveNode(node, origin);
        }

        if (_selectionBefore is { } before)
        {
            RestoreSelection(before.Nodes, before.Beam);
        }

        ResetTool();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void StartToolGesture(int pointer, Vector2D viewPosition)
    {
        ResetTool();
        _toolPointer = pointer;
        _pressTool = _construction.ActiveTool;
        _pressViewPosition = viewPosition;
        _lastViewPosition = viewPosition;
        var position = View.ToCanvas(viewPosition);
        _pressPosition = position;
        if (_pressTool == ConstructionTool.Select && FindHandle(viewPosition) is { } handle)
        {
            _pressedHandle = handle;
        }

        if (_construction.TryFindNodeNear(position, HitDistance(NodeHitRadius), out var nodeIndex))
        {
            _pressedNode = nodeIndex;
        }
        else if (_pressedHandle is null && _construction.TryFindBeamNear(position, HitDistance(BeamHitDistance), out var beamIndex))
        {
            _pressedBeam = beamIndex;
        }

        switch (_pressTool)
        {
            case ConstructionTool.Beam when _pressedNode is { } start && !_construction.IsMoveOnly:
                BeamStartNode = start;
                BeamEnd = position;
                Changed?.Invoke(this, EventArgs.Empty);
                break;
            case ConstructionTool.Select when _pressedHandle is null:
                _selectionBefore = ([.. _construction.SelectedNodeIndices], _construction.SingleSelectedBeamIndex);
                PressSelect(viewPosition, position);
                break;
        }
    }

    private void DragTool(Vector2D viewPosition)
    {
        if (_construction.ActiveTool != _pressTool)
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
            if (_pressTool == ConstructionTool.Select && (_pressedHandle is not null || _pressedNode is not null))
            {
                _selectionStart = _construction.SnapshotSelection();
                NodeDragStarting?.Invoke(this, [.. _selectionStart.Positions.Keys]);
            }
            else if (_pressTool == ConstructionTool.Move && _pressedNode is { } dragged)
            {
                _dragOrigin = _construction.Nodes[dragged].Position;
                NodeDragStarting?.Invoke(this, [dragged]);
            }
        }

        var lastViewPosition = _lastViewPosition;
        _lastViewPosition = viewPosition;
        var position = View.ToCanvas(viewPosition);
        switch (_pressTool)
        {
            case ConstructionTool.Move when _pressedNode is { } node:
                _construction.MoveNode(node, position);
                break;
            case ConstructionTool.Move:
                View.PanBy(new Vector2D(viewPosition.X - lastViewPosition.X, viewPosition.Y - lastViewPosition.Y));
                break;
            case ConstructionTool.Beam when BeamStartNode is { } start:
                BeamEnd = position;
                BeamTargetNode = FindBeamTarget(start, position);
                Changed?.Invoke(this, EventArgs.Empty);
                break;
            case ConstructionTool.Select when _selectionStart is { } start:
                TransformSelection(start, position);
                break;
            case ConstructionTool.Select when SelectionBox is { } box:
                SelectionBox = (box.Start, position);
                Changed?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private void ReleaseTool(Vector2D viewPosition)
    {
        if (_construction.ActiveTool != _pressTool)
        {
            Cancel();
            return;
        }

        var position = View.ToCanvas(viewPosition);
        switch (_pressTool)
        {
            case ConstructionTool.Move when !_dragging:
                TapMove();
                break;
            case ConstructionTool.Beam when BeamStartNode is { } start && FindBeamTarget(start, position) is { } end:
                _construction.ConnectBeam(start, end);
                break;
            case ConstructionTool.Joint when !_dragging:
                TapJoint();
                break;
            case ConstructionTool.Select when SelectionBox is { } box:
                CompleteSelectionBox(box.Start, position);
                break;
            case ConstructionTool.Select when !_dragging && (_pressedNodeWasSelected || _pressedHandle is not null) && _pressedNode is { } tapped:
                // A handle drags; a tap on a joint under it still adds or removes that joint.
                _construction.ToggleSelectedNode(tapped);
                break;
            case ConstructionTool.Core when !_dragging && _pressedNode is { } coreNode:
                _construction.ToggleCoreOnNode(coreNode);
                break;
        }

        ResetTool();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RestoreSelection(int[] nodes, int? beam)
    {
        if (beam is { } selectedBeam)
        {
            if (_construction.SingleSelectedBeamIndex != selectedBeam)
            {
                _construction.SelectBeam(selectedBeam);
            }
        }
        else if (_construction.SelectedBeamCount != 0
            || _construction.SelectedNodeIndices.Count != nodes.Length
            || !nodes.All(_construction.SelectedNodeIndices.Contains))
        {
            _construction.ReplaceSelection(nodes);
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
    /// else, beams included, clears the selection and starts a box.
    /// </summary>
    private void PressSelect(Vector2D viewPosition, Vector2D position)
    {
        if (_pressedNode is { } node)
        {
            _pressedNodeWasSelected = _construction.SelectedNodeIndices.Contains(node);
            if (!_pressedNodeWasSelected)
            {
                _construction.ToggleSelectedNode(node);
            }
        }
        else if (FrameInView() is { } frame && frame.Contains(viewPosition))
        {
            _pressedHandle = SelectionHandle.Move;
        }
        else
        {
            _construction.ClearSelection();
            SelectionBox = (position, position);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void TapMove()
    {
        if (_pressedNode is { } node)
        {
            _construction.ReplaceSelection([node]);
        }
        else if (_pressedBeam is { } beam)
        {
            _construction.SelectBeam(beam);
        }
        else
        {
            _construction.ClearSelection();
        }
    }

    private void TapJoint()
    {
        if (_pressedNode is not null || _construction.IsMoveOnly)
        {
            return;
        }

        if (_pressedBeam is { } beam)
        {
            _construction.SplitBeam(beam, _pressPosition, NewNodeRadius);
        }
        else if (ConstructionViewModel.BuildArea.Contains(_pressPosition))
        {
            _construction.PlaceNode(_pressPosition, NewNodeRadius);
        }
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
                _construction.RotateSelection(start, Math.Atan2(to.Y, to.X) - Math.Atan2(from.Y, from.X));
                break;
            case SelectionHandle.Scale when (from.X * from.X) + (from.Y * from.Y) > 0:
                // Only the drag along the handle's diagonal counts, so the group never reflects.
                _construction.ScaleSelection(start, ((to.X * from.X) + (to.Y * from.Y)) / ((from.X * from.X) + (from.Y * from.Y)));
                break;
            case SelectionHandle.Scale:
                break;
            default:
                _construction.TranslateSelection(start, new Vector2D(position.X - _pressPosition.X, position.Y - _pressPosition.Y));
                break;
        }
    }

    /// <summary>The frame in view units, while Select has two or more joints and no box is being dragged.</summary>
    private CanvasRect? FrameInView()
    {
        if (_construction.ActiveTool != ConstructionTool.Select || _construction.SelectedNodeCount < 2 || SelectionBox is not null)
        {
            return null;
        }

        var nodes = _construction.SelectedNodeIndices.Select(index => _construction.Nodes[index]).ToArray();
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
        if (FrameInView() is not { } frame)
        {
            yield break;
        }

        yield return (SelectionHandle.Move, frame.Center);
        yield return (SelectionHandle.Rotate, new Vector2D(frame.Center.X, frame.Min.Y - _rotateStem));
        yield return (SelectionHandle.Scale, frame.Max);
    }

    private SelectionHandle? FindHandle(Vector2D viewPosition) =>
        HandlesInView()
            .Where(entry => Distance(entry.Position, viewPosition) <= HandleHitRadius)
            .OrderBy(entry => Distance(entry.Position, viewPosition))
            .Select(entry => (SelectionHandle?)entry.Handle)
            .FirstOrDefault();

    /// <summary>Snaps only to joints the beam could actually join, so the preview never promises a refused connection.</summary>
    private int? FindBeamTarget(int start, Vector2D position) =>
        _construction.TryFindNodeNear(position, HitDistance(NodeHitRadius), out var end) && _construction.CanConnect(start, end) ? end : null;

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
        for (var i = 0; i < _construction.Nodes.Count; i++)
        {
            var node = _construction.Nodes[i].Position;
            if (node.X >= minX && node.X <= maxX && node.Y >= minY && node.Y <= maxY)
            {
                selected.Add(i);
            }
        }

        _construction.ReplaceSelection(selected);
    }

    private double HitDistance(double viewDistance) => viewDistance / View.Zoom;

    /// <summary>What the view fits on open: every node with room for its motor arc (twice its radius).</summary>
    private CanvasRect? ContentBounds()
    {
        var nodes = _construction.Nodes;
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
        _pressedNode = null;
        _pressedBeam = null;
        BeamStartNode = null;
        BeamEnd = null;
        BeamTargetNode = null;
        SelectionBox = null;
    }

    private static Vector2D Midpoint(Vector2D a, Vector2D b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);

    private static double Distance(Vector2D a, Vector2D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
