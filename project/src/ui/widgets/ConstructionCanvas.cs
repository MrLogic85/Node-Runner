using System.ComponentModel;
using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// Renders the anatomy placed so far in construction mode and lets the user
/// edit it with touch, per the active <see cref="ConstructionTool"/>: place
/// or drag nodes, connect two nodes with a beam, attach/remove a core, or
/// delete a node/beam. Binds to <see cref="ConstructionViewModel"/> per
/// `project/src/ui/AGENTS.md`; does not own any anatomy state itself. See
/// docs/CONSTRUCTION_MODE.md.
/// </summary>
public partial class ConstructionCanvas : Node2D
{
    private const float _nodeHitRadius = 32f;
    private const float _defaultNodeRadius = 18f;
    private const float _beamHitDistance = 20f;

    private ConstructionViewModel? _viewModel;
    private int _draggingNodeIndex = -1;
    private readonly Dictionary<int, Vector2D> _ghostNodePositions = [];
    private int _ghostVersion;
    private Vector2? _selectionBoxStart;
    private Vector2? _selectionBoxCurrent;

    public VisualTheme Theme { get; set; } = VisualTheme.Neon;

    public ConstructionViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            if (_viewModel is not null)
            {
                _viewModel.AnatomyChanged -= OnAnatomyChanged;
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _viewModel = value;
            ClearMoveGhosts();
            if (_viewModel is not null)
            {
                _viewModel.AnatomyChanged += OnAnatomyChanged;
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            }

            QueueRedraw();
        }
    }

    public override void _ExitTree()
    {
        if (_viewModel is not null)
        {
            _viewModel.AnatomyChanged -= OnAnatomyChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_viewModel is null || !_viewModel.IsActive)
        {
            return;
        }

        if (PointerInput.TryGetPressPosition(inputEvent, out var pressPosition))
        {
            HandlePress(ToCanvasLocal(pressPosition));
            GetViewport().SetInputAsHandled();
            return;
        }

        if (PointerInput.TryGetDragPosition(inputEvent, out var dragPosition))
        {
            if (_selectionBoxStart is not null)
            {
                _selectionBoxCurrent = ToCanvasLocal(dragPosition);
                QueueRedraw();
                GetViewport().SetInputAsHandled();
            }
            else if (_draggingNodeIndex >= 0)
            {
                HandleDrag(ToCanvasLocal(dragPosition));
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (PointerInput.TryGetReleasePosition(inputEvent, out _))
        {
            CompleteSelectionBox();
            _draggingNodeIndex = -1;
            ScheduleMoveGhostClear();
        }
    }

    public override void _Draw()
    {
        if (_viewModel is null)
        {
            return;
        }

        DrawMoveGhosts();
        DrawSelectionBox();

        foreach (var beam in _viewModel.Beams)
        {
            var start = ToGodot(_viewModel.Nodes[beam.NodeA].Position);
            var end = ToGodot(_viewModel.Nodes[beam.NodeB].Position);
            if (_viewModel.SingleSelectedBeamIndex is { } selectedBeamIndex && _viewModel.Beams[selectedBeamIndex] == beam)
            {
                DrawLine(start, end, Theme.SelectionGlow, Theme.BeamWidth * 2.2f, antialiased: true);
            }
            DrawLine(start, end, Theme.Beam, Theme.BeamWidth, antialiased: true);
        }

        DrawTopologyFeedback();

        for (var nodeIndex = 0; nodeIndex < _viewModel.Nodes.Count; nodeIndex++)
        {
            var node = _viewModel.Nodes[nodeIndex];
            var position = ToGodot(node.Position);
            if (_viewModel.SelectedNodeIndices.Contains(nodeIndex))
            {
                DrawCircle(position, (float)node.Radius * 1.7f, Theme.SelectionGlow);
            }

            DrawCircle(position, (float)node.Radius * 1.18f, Theme.NodeGlow);
            DrawCircle(position, (float)node.Radius, Theme.NodeFill);
        }

        DrawInvalidNodeMarkers();
        DrawInvalidBeamMarkers();

        foreach (var core in _viewModel.Cores)
        {
            var position = ToGodot(_viewModel.Nodes[core.NodeIndex].Position);
            var radius = (float)_viewModel.Nodes[core.NodeIndex].Radius;
            DrawCircle(position, radius * 0.42f, Theme.CoreMarker);
        }

        DrawMotorCenterMarkers();

        if (_viewModel.PendingBeamStartNode is { } pendingIndex)
        {
            var position = ToGodot(_viewModel.Nodes[pendingIndex].Position);
            var radius = (float)_viewModel.Nodes[pendingIndex].Radius;
            DrawCircle(position, radius * 1.65f, Theme.SelectionGlow);
        }
    }

    private void DrawSelectionBox()
    {
        if (_selectionBoxStart is not { } start || _selectionBoxCurrent is not { } current)
        {
            return;
        }

        var rect = RectFromPoints(start, current);
        var fill = Theme.SelectionGlow;
        fill.A = 0.16f;
        DrawRect(rect, fill, filled: true);
        DrawDashedLine(rect.Position, rect.Position + new Vector2(rect.Size.X, 0), Theme.SelectionGlow, 2, 6, antialiased: true);
        DrawDashedLine(rect.Position + new Vector2(rect.Size.X, 0), rect.End, Theme.SelectionGlow, 2, 6, antialiased: true);
        DrawDashedLine(rect.End, rect.Position + new Vector2(0, rect.Size.Y), Theme.SelectionGlow, 2, 6, antialiased: true);
        DrawDashedLine(rect.Position + new Vector2(0, rect.Size.Y), rect.Position, Theme.SelectionGlow, 2, 6, antialiased: true);
    }

    private void DrawMoveGhosts()
    {
        if (_viewModel is null || _ghostNodePositions.Count == 0)
        {
            return;
        }

        foreach (var beam in _viewModel.Beams)
        {
            if (!_ghostNodePositions.TryGetValue(beam.NodeA, out var startPosition)
                && !_ghostNodePositions.TryGetValue(beam.NodeB, out var endPosition))
            {
                continue;
            }

            startPosition = _ghostNodePositions.GetValueOrDefault(beam.NodeA, _viewModel.Nodes[beam.NodeA].Position);
            endPosition = _ghostNodePositions.GetValueOrDefault(beam.NodeB, _viewModel.Nodes[beam.NodeB].Position);
            DrawDashedLine(ToGodot(startPosition), ToGodot(endPosition), Theme.SelectionGlow, 4, 8, antialiased: true);
        }

        foreach (var (nodeIndex, position) in _ghostNodePositions)
        {
            if (nodeIndex < 0 || nodeIndex >= _viewModel.Nodes.Count)
            {
                continue;
            }

            var radius = (float)_viewModel.Nodes[nodeIndex].Radius;
            DrawArc(ToGodot(position), radius * 1.35f, 0, Mathf.Tau, 32, Theme.SelectionGlow, 2, antialiased: true);
        }
    }

    private void DrawTopologyFeedback()
    {
        if (!TryBuildDrawableTopology(out var creature) || creature is null)
        {
            return;
        }

        foreach (var triangle in MotorTopology.BuildRigidTriangles(creature))
        {
            DrawRigidTriangle(creature, triangle);
        }

        foreach (var connection in MotorTopology.BuildNodeConnections(creature).Where(connection => connection.IsMotorized))
        {
            DrawMotorRelation(creature, connection);
        }
    }

    private bool TryBuildDrawableTopology(out CreatureDef? creature)
    {
        creature = null;
        if (_viewModel is null)
        {
            return false;
        }

        var drawableBeamSourceIndices = new List<int>();
        var drawableNodeSourceIndices = new SortedSet<int>();
        for (var beamIndex = 0; beamIndex < _viewModel.Beams.Count; beamIndex++)
        {
            var beam = _viewModel.Beams[beamIndex];
            if (_viewModel.Nodes[beam.NodeA].Position == _viewModel.Nodes[beam.NodeB].Position)
            {
                continue;
            }

            drawableBeamSourceIndices.Add(beamIndex);
            drawableNodeSourceIndices.Add(beam.NodeA);
            drawableNodeSourceIndices.Add(beam.NodeB);
        }

        if (drawableBeamSourceIndices.Count == 0)
        {
            return false;
        }

        var nodeMap = drawableNodeSourceIndices
            .Select((sourceIndex, drawableIndex) => (sourceIndex, drawableIndex))
            .ToDictionary(pair => pair.sourceIndex, pair => pair.drawableIndex);
        var nodes = drawableNodeSourceIndices
            .Select(sourceIndex => _viewModel.Nodes[sourceIndex])
            .ToArray();
        var beams = drawableBeamSourceIndices
            .Select(beamIndex =>
            {
                var beam = _viewModel.Beams[beamIndex];
                return new BeamDef(nodeMap[beam.NodeA], nodeMap[beam.NodeB]);
            })
            .ToArray();
        var cores = _viewModel.Cores
            .Where(core => nodeMap.ContainsKey(core.NodeIndex))
            .Select(core => new CoreDef(nodeMap[core.NodeIndex]))
            .ToArray();

        try
        {
            creature = new CreatureDef(nodes, beams, cores);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private void DrawRigidTriangle(CreatureDef creature, RigidTriangleDef triangle)
    {
        var a = ToGodot(creature.Nodes[triangle.NodeA].Position);
        var b = ToGodot(creature.Nodes[triangle.NodeB].Position);
        var c = ToGodot(creature.Nodes[triangle.NodeC].Position);
        var fill = new Color(Theme.SelectionGlow.R, Theme.SelectionGlow.G, Theme.SelectionGlow.B, 0.10f);
        DrawColoredPolygon([a, b, c], fill);

        var center = (a + b + c) / 3f;
        DrawLine(a.Lerp(center, 0.35f), b.Lerp(center, 0.35f), Theme.SelectionGlow, 2, antialiased: true);
        DrawLine(b.Lerp(center, 0.35f), c.Lerp(center, 0.35f), Theme.SelectionGlow, 2, antialiased: true);
        DrawLine(c.Lerp(center, 0.35f), a.Lerp(center, 0.35f), Theme.SelectionGlow, 2, antialiased: true);

        var labelPosition = center + new Vector2(12, -12);
        DrawRect(new Rect2(labelPosition + new Vector2(-6, -22), new Vector2(168, 30)), new Color(0.01f, 0.02f, 0.05f, 0.86f));
        DrawString(ThemeDB.FallbackFont, labelPosition, "Rigid: no joints", HorizontalAlignment.Left, -1, 18, Theme.GroundEdge);
    }

    private void DrawMotorRelation(CreatureDef creature, NodeConnectionDef connection)
    {
        var node = creature.Nodes[connection.NodeIndex];
        var center = ToGodot(node.Position);
        var start = BeamAngleFromNode(creature, creature.Beams[connection.ReferenceBeamIndex], connection.NodeIndex);
        var end = BeamAngleFromNode(creature, creature.Beams[connection.OtherBeamIndex], connection.NodeIndex);
        var delta = Mathf.Wrap(end - start, -Mathf.Pi, Mathf.Pi);
        var arcStart = delta < 0 ? start + delta : start;
        var arcEnd = delta < 0 ? start : start + delta;

        var radius = (float)node.Radius * 2.0f;
        DrawArc(center, radius, arcStart, arcEnd, 28, Theme.MotorAccent, Theme.MotorSignalWidth, antialiased: true);
    }

    private void DrawMotorCenterMarkers()
    {
        if (!TryBuildDrawableTopology(out var creature) || creature is null)
        {
            return;
        }

        foreach (var nodeIndex in MotorTopology.BuildNodeConnections(creature)
            .Where(connection => connection.IsMotorized)
            .Select(connection => connection.NodeIndex)
            .Distinct())
        {
            var node = creature.Nodes[nodeIndex];
            DrawArc(ToGodot(node.Position), (float)node.Radius * 0.72f, 0, Mathf.Tau, 32, Theme.MotorAccent, Theme.MotorSignalWidth, antialiased: true);
        }
    }

    private static float BeamAngleFromNode(CreatureDef creature, BeamDef beam, int nodeIndex)
    {
        var otherNodeIndex = beam.NodeA == nodeIndex ? beam.NodeB : beam.NodeA;
        var node = creature.Nodes[nodeIndex].Position;
        var other = creature.Nodes[otherNodeIndex].Position;
        return Mathf.Atan2((float)(other.Y - node.Y), (float)(other.X - node.X));
    }

    private void DrawInvalidNodeMarkers()
    {
        if (_viewModel is null)
        {
            return;
        }

        for (var nodeIndex = 0; nodeIndex < _viewModel.Nodes.Count; nodeIndex++)
        {
            if (_viewModel.Beams.Any(beam => beam.NodeA == nodeIndex || beam.NodeB == nodeIndex))
            {
                continue;
            }

            var node = _viewModel.Nodes[nodeIndex];
            var position = ToGodot(node.Position);
            var radius = (float)node.Radius * 1.55f;
            DrawArc(position, radius, 0, Mathf.Tau, 32, Theme.Danger, Theme.MotorSignalWidth, antialiased: true);
            DrawLine(position + new Vector2(-radius * 0.45f, -radius * 0.45f), position + new Vector2(radius * 0.45f, radius * 0.45f), Theme.Danger, Theme.MotorSignalWidth, antialiased: true);
            DrawLine(position + new Vector2(radius * 0.45f, -radius * 0.45f), position + new Vector2(-radius * 0.45f, radius * 0.45f), Theme.Danger, Theme.MotorSignalWidth, antialiased: true);
        }
    }

    private void DrawInvalidBeamMarkers()
    {
        if (_viewModel is null)
        {
            return;
        }

        foreach (var beam in _viewModel.Beams)
        {
            var start = _viewModel.Nodes[beam.NodeA];
            var end = _viewModel.Nodes[beam.NodeB];
            if (start.Position != end.Position)
            {
                continue;
            }

            var position = ToGodot(start.Position);
            var radius = (float)Math.Max(start.Radius, end.Radius) * 1.95f;
            DrawArc(position, radius, 0, Mathf.Tau, 32, Theme.Danger, Theme.MotorSignalWidth, antialiased: true);
            DrawLine(position + new Vector2(-radius * 0.55f, 0), position + new Vector2(radius * 0.55f, 0), Theme.Danger, Theme.MotorSignalWidth, antialiased: true);
            DrawLine(position + new Vector2(0, -radius * 0.55f), position + new Vector2(0, radius * 0.55f), Theme.Danger, Theme.MotorSignalWidth, antialiased: true);
        }
    }

    private void HandlePress(Vector2 localPosition)
    {
        if (_viewModel is null)
        {
            return;
        }

        var domainPosition = ToDomain(localPosition);
        var foundNode = _viewModel.TryFindNodeNear(domainPosition, _nodeHitRadius, out var nodeIndex);

        switch (_viewModel.ActiveTool)
        {
            case ConstructionTool.Beam:
                if (foundNode)
                {
                    _viewModel.SelectNodeForBeam(nodeIndex);
                }

                break;
            case ConstructionTool.Core:
                if (foundNode)
                {
                    _viewModel.ToggleCoreOnNode(nodeIndex);
                }

                break;
            case ConstructionTool.Delete:
                if (foundNode)
                {
                    _viewModel.DeleteNode(nodeIndex);
                }
                else if (_viewModel.TryFindBeamNear(domainPosition, _beamHitDistance, out var beamIndex))
                {
                    _viewModel.DeleteBeam(beamIndex);
                }

                break;
            case ConstructionTool.Select:
                if (foundNode)
                {
                    if (!_viewModel.SelectedNodeIndices.Contains(nodeIndex))
                    {
                        _viewModel.ToggleSelectedNode(nodeIndex);
                    }

                    CaptureMoveGhosts(nodeIndex);
                    _draggingNodeIndex = nodeIndex;
                }
                else
                {
                    if (_viewModel.TryFindBeamNear(domainPosition, _beamHitDistance, out var beamIndex))
                    {
                        _viewModel.SelectBeam(beamIndex);
                    }
                    else
                    {
                        _viewModel.ClearSelection();
                        _selectionBoxStart = localPosition;
                        _selectionBoxCurrent = localPosition;
                    }
                }

                break;
            case ConstructionTool.Place:
            default:
                if (foundNode)
                {
                    if (_viewModel.IsMoveOnly)
                    {
                        _viewModel.ClearSelection();
                        _viewModel.ToggleSelectedNode(nodeIndex);
                        CaptureMoveGhosts(nodeIndex);
                    }

                    _draggingNodeIndex = nodeIndex;
                }
                else
                {
                    if (_viewModel.IsMoveOnly)
                    {
                        if (_viewModel.TryFindBeamNear(domainPosition, _beamHitDistance, out var beamIndex))
                        {
                            _viewModel.SelectBeam(beamIndex);
                        }
                        else
                        {
                            _viewModel.ClearSelection();
                        }
                    }
                    else
                    {
                        _draggingNodeIndex = _viewModel.PlaceNode(domainPosition, _defaultNodeRadius);
                    }
                }

                break;
        }
    }

    private void CaptureMoveGhosts(int anchorNodeIndex)
    {
        if (_viewModel is null || !_viewModel.IsMoveOnly)
        {
            return;
        }

        _ghostNodePositions.Clear();
        _ghostVersion++;
        var indices = _viewModel.SelectedNodeIndices.Count > 0
            ? _viewModel.SelectedNodeIndices
            : [anchorNodeIndex];
        foreach (var index in indices)
        {
            if (index >= 0 && index < _viewModel.Nodes.Count)
            {
                _ghostNodePositions[index] = _viewModel.Nodes[index].Position;
            }
        }
    }

    private void CompleteSelectionBox()
    {
        if (_viewModel is null || _selectionBoxStart is not { } start || _selectionBoxCurrent is not { } current)
        {
            _selectionBoxStart = null;
            _selectionBoxCurrent = null;
            return;
        }

        var rect = RectFromPoints(start, current);
        if (rect.Size.X < 8 && rect.Size.Y < 8)
        {
            _selectionBoxStart = null;
            _selectionBoxCurrent = null;
            QueueRedraw();
            return;
        }

        var selected = new List<int>();
        for (var i = 0; i < _viewModel.Nodes.Count; i++)
        {
            if (rect.HasPoint(ToGodot(_viewModel.Nodes[i].Position)))
            {
                selected.Add(i);
            }
        }

        _viewModel.ReplaceSelection(selected);
        _selectionBoxStart = null;
        _selectionBoxCurrent = null;
        QueueRedraw();
    }

    private static Rect2 RectFromPoints(Vector2 first, Vector2 second)
    {
        var min = new Vector2(Mathf.Min(first.X, second.X), Mathf.Min(first.Y, second.Y));
        var max = new Vector2(Mathf.Max(first.X, second.X), Mathf.Max(first.Y, second.Y));
        return new Rect2(min, max - min);
    }

    private void ScheduleMoveGhostClear()
    {
        if (_ghostNodePositions.Count == 0 || GetTree() is not { } tree)
        {
            return;
        }

        var version = _ghostVersion;
        tree.CreateTimer(1.8).Timeout += () =>
        {
            if (_ghostVersion != version)
            {
                return;
            }

            ClearMoveGhosts();
        };
    }

    private void ClearMoveGhosts()
    {
        if (_ghostNodePositions.Count == 0)
        {
            return;
        }

        _ghostVersion++;
        _ghostNodePositions.Clear();
        QueueRedraw();
    }

    private void HandleDrag(Vector2 localPosition)
    {
        if (_viewModel is null || (_viewModel.ActiveTool != ConstructionTool.Place && _viewModel.ActiveTool != ConstructionTool.Select))
        {
            return;
        }

        if (_draggingNodeIndex < 0 || _draggingNodeIndex >= _viewModel.Nodes.Count)
        {
            return;
        }

        if (_viewModel.ActiveTool == ConstructionTool.Select)
        {
            _viewModel.MoveSelectedNodes(_draggingNodeIndex, ToDomain(localPosition));
        }
        else
        {
            _viewModel.MoveNode(_draggingNodeIndex, ToDomain(localPosition));
        }
    }

    private void OnAnatomyChanged(object? sender, EventArgs eventArgs)
    {
        QueueRedraw();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(ConstructionViewModel.PendingBeamStartNode)
            || eventArgs.PropertyName == nameof(ConstructionViewModel.SelectedNodeCount))
        {
            QueueRedraw();
        }

        if (eventArgs.PropertyName == nameof(ConstructionViewModel.IsMoveOnly) && _viewModel?.IsMoveOnly != true)
        {
            ClearMoveGhosts();
        }
    }

    private Vector2 ToCanvasLocal(Vector2 screenPosition)
    {
        // Regular ToLocal()/GetGlobalTransform() do not include the
        // viewport's stretch transform (see [display] in project.godot), so
        // raw screen-pixel input positions need GetGlobalTransformWithCanvas
        // to land on the right spot. Mirrors Main._UnhandledInput's
        // creature-selection math.
        return GetGlobalTransformWithCanvas().AffineInverse() * screenPosition;
    }

    private static Vector2D ToDomain(Vector2 position)
    {
        return new Vector2D(position.X, position.Y);
    }

    private static Vector2 ToGodot(Vector2D position)
    {
        return new Vector2((float)position.X, (float)position.Y);
    }
}
