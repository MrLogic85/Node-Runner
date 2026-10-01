using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// Turns one pointer's press, drag and release on the Build canvas into edits
/// for the active <see cref="ConstructionTool"/>, so the canvas only converts
/// input to canvas coordinates and draws. Positions are in canvas units, the
/// same as <see cref="NodeDef.Position"/>. See `docs/CONSTRUCTION_MODE.md`.
/// </summary>
public sealed class ConstructionGestures
{
    public const double NodeHitRadius = 32;
    public const double BeamHitDistance = 20;
    public const double NewNodeRadius = 18;

    /// <summary>How far a pointer may travel and still count as a tap.</summary>
    public const double TapSlop = 8;

    private const double _selectionBoxMinSize = 8;

    private readonly ConstructionViewModel _construction;
    private bool _pressed;
    private ConstructionTool _pressTool;
    private Vector2D _pressPosition;
    private bool _dragging;
    private int? _pressedNode;
    private int? _pressedBeam;

    public ConstructionGestures(ConstructionViewModel construction)
    {
        _construction = construction ?? throw new ArgumentNullException(nameof(construction));
    }

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

    public void Press(Vector2D position)
    {
        Reset();
        _pressed = true;
        _pressTool = _construction.ActiveTool;
        _pressPosition = position;
        if (_construction.TryFindNodeNear(position, NodeHitRadius, out var nodeIndex))
        {
            _pressedNode = nodeIndex;
        }
        else if (_construction.TryFindBeamNear(position, BeamHitDistance, out var beamIndex))
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
            case ConstructionTool.Select:
                PressSelect(position);
                break;
            case ConstructionTool.Core when _pressedNode is { } coreNode:
                _construction.ToggleCoreOnNode(coreNode);
                break;
        }
    }

    public void Drag(Vector2D position)
    {
        if (!_pressed)
        {
            return;
        }

        if (_construction.ActiveTool != _pressTool)
        {
            Cancel();
            return;
        }

        if (!_dragging)
        {
            if (Distance(position, _pressPosition) <= TapSlop)
            {
                return;
            }

            _dragging = true;
            if (_pressedNode is { } dragged && (_pressTool == ConstructionTool.Move || _pressTool == ConstructionTool.Select))
            {
                NodeDragStarting?.Invoke(this, NodesMovedBy(dragged));
            }
        }

        switch (_pressTool)
        {
            case ConstructionTool.Move when _pressedNode is { } node:
                _construction.MoveNode(node, position);
                break;
            case ConstructionTool.Beam when BeamStartNode is { } start:
                BeamEnd = position;
                BeamTargetNode = FindBeamTarget(start, position);
                Changed?.Invoke(this, EventArgs.Empty);
                break;
            case ConstructionTool.Select when _pressedNode is { } anchor:
                _construction.MoveSelectedNodes(anchor, position);
                break;
            case ConstructionTool.Select when SelectionBox is { } box:
                SelectionBox = (box.Start, position);
                Changed?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    public void Release(Vector2D position)
    {
        if (!_pressed)
        {
            return;
        }

        if (_construction.ActiveTool != _pressTool)
        {
            Cancel();
            return;
        }

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
        }

        Reset();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Drops the gesture in progress without any further edit.</summary>
    public void Cancel()
    {
        if (!_pressed)
        {
            return;
        }

        Reset();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void PressSelect(Vector2D position)
    {
        if (_pressedNode is { } node)
        {
            if (!_construction.SelectedNodeIndices.Contains(node))
            {
                _construction.ToggleSelectedNode(node);
            }
        }
        else if (_pressedBeam is { } beam)
        {
            _construction.SelectBeam(beam);
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
        else
        {
            _construction.PlaceNode(_pressPosition, NewNodeRadius);
        }
    }

    /// <summary>A Select drag moves the whole selection when it grabs a selected node (<see cref="ConstructionViewModel.MoveSelectedNodes"/>); every other drag moves one node.</summary>
    private IReadOnlyCollection<int> NodesMovedBy(int dragged) =>
        _pressTool == ConstructionTool.Select && _construction.SelectedNodeIndices.Contains(dragged)
            ? [.. _construction.SelectedNodeIndices]
            : [dragged];

    /// <summary>Snaps only to joints the beam could actually join, so the preview never promises a refused connection.</summary>
    private int? FindBeamTarget(int start, Vector2D position) =>
        _construction.TryFindNodeNear(position, NodeHitRadius, out var end) && _construction.CanConnect(start, end) ? end : null;

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

    private void Reset()
    {
        _pressed = false;
        _dragging = false;
        _pressedNode = null;
        _pressedBeam = null;
        BeamStartNode = null;
        BeamEnd = null;
        BeamTargetNode = null;
        SelectionBox = null;
    }

    private static double Distance(Vector2D a, Vector2D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
