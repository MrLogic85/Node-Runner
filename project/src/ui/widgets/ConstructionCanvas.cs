using System.ComponentModel;
using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// Renders the anatomy placed so far in Build and forwards touch to
/// <see cref="ConstructionGestures"/>, which decides what the active
/// <see cref="ConstructionTool"/> does. Binds to
/// <see cref="ConstructionViewModel"/> per `project/src/ui/AGENTS.md`; does
/// not own any anatomy state itself. See docs/CONSTRUCTION_MODE.md.
/// </summary>
public partial class ConstructionCanvas : Node2D
{
    private const double _moveGhostSeconds = 1.8;
    private static readonly Vector2 _rigidLabelOffset = new(12, -12);
    private static readonly Rect2 _rigidLabelBox = new(-6, -22, 168, 30);
    private const int _rigidLabelFontSize = 18;

    private ConstructionViewModel? _viewModel;
    private ConstructionGestures? _gestures;
    private readonly Dictionary<int, Vector2D> _ghostNodePositions = [];
    private int _ghostVersion;

    public VisualTheme Theme { get; set; } = VisualTheme.Neon;

    public ConstructionViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            Unbind();
            _viewModel = value;
            ClearMoveGhosts();
            if (_viewModel is not null)
            {
                _viewModel.AnatomyChanged += OnAnatomyChanged;
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                _gestures = new ConstructionGestures(_viewModel);
                _gestures.Changed += OnGesturesChanged;
                _gestures.NodeDragStarting += OnNodeDragStarting;
            }

            QueueRedraw();
        }
    }

    public override void _ExitTree() => Unbind();

    private void Unbind()
    {
        if (_viewModel is not null)
        {
            _viewModel.AnatomyChanged -= OnAnatomyChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (_gestures is not null)
        {
            _gestures.Changed -= OnGesturesChanged;
            _gestures.NodeDragStarting -= OnNodeDragStarting;
            _gestures = null;
        }
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_viewModel is null || _gestures is null || !_viewModel.IsActive)
        {
            return;
        }

        if (PointerInput.TryGetPressPosition(inputEvent, out var pressPosition))
        {
            _gestures.Press(ToDomain(ToCanvasLocal(pressPosition)));
            GetViewport().SetInputAsHandled();
        }
        else if (PointerInput.TryGetDragPosition(inputEvent, out var dragPosition))
        {
            _gestures.Drag(ToDomain(ToCanvasLocal(dragPosition)));
            GetViewport().SetInputAsHandled();
        }
        else if (PointerInput.TryGetReleasePosition(inputEvent, out var releasePosition))
        {
            _gestures.Release(ToDomain(ToCanvasLocal(releasePosition)));
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
        DrawBeamPreview();

        foreach (var beam in _viewModel.Beams)
        {
            var start = ToGodot(_viewModel.Nodes[beam.NodeA].Position);
            var end = ToGodot(_viewModel.Nodes[beam.NodeB].Position);
            if (_viewModel.SingleSelectedBeamIndex is { } selectedBeamIndex && _viewModel.Beams[selectedBeamIndex] == beam)
            {
                DrawLine(start, end, Theme.SelectionGlow, Theme.BeamWidth * 2.2f, antialiased: false);
            }
            DrawLine(start, end, Theme.Beam, Theme.BeamWidth, antialiased: false);
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

            DrawCircle(
                position,
                (float)node.Radius * 1.18f,
                UiGlow.FromBase(Theme.GroundEdge, Theme.EffectsEnabled));
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

        DrawBeamEndRings();
    }

    private void DrawBeamPreview()
    {
        if (_viewModel is null || _gestures?.BeamStartNode is not { } start || _gestures.BeamEnd is not { } end)
        {
            return;
        }

        var to = _gestures.BeamTargetNode is { } target ? _viewModel.Nodes[target].Position : end;
        DrawDashedLine(ToGodot(_viewModel.Nodes[start].Position), ToGodot(to), Theme.SelectionGlow, Theme.BeamWidth, 8, antialiased: false);
    }

    private void DrawBeamEndRings()
    {
        if (_viewModel is null || _gestures is null)
        {
            return;
        }

        foreach (var nodeIndex in new[] { _gestures.BeamStartNode, _gestures.BeamTargetNode })
        {
            if (nodeIndex is { } index)
            {
                var node = _viewModel.Nodes[index];
                DrawArc(ToGodot(node.Position), (float)node.Radius * 1.65f, 0, Mathf.Tau, 32, Theme.SelectionGlow, Theme.MotorSignalWidth, antialiased: false);
            }
        }
    }

    private void DrawSelectionBox()
    {
        if (_gestures?.SelectionBox is not { } box)
        {
            return;
        }

        var rect = RectFromPoints(ToGodot(box.Start), ToGodot(box.End));
        var fill = Theme.SelectionGlow;
        fill.A = 0.16f;
        DrawRect(rect, fill, filled: true);
        DrawDashedLine(rect.Position, rect.Position + new Vector2(rect.Size.X, 0), Theme.SelectionGlow, 2, 6, antialiased: false);
        DrawDashedLine(rect.Position + new Vector2(rect.Size.X, 0), rect.End, Theme.SelectionGlow, 2, 6, antialiased: false);
        DrawDashedLine(rect.End, rect.Position + new Vector2(0, rect.Size.Y), Theme.SelectionGlow, 2, 6, antialiased: false);
        DrawDashedLine(rect.Position + new Vector2(0, rect.Size.Y), rect.Position, Theme.SelectionGlow, 2, 6, antialiased: false);
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
            DrawDashedLine(ToGodot(startPosition), ToGodot(endPosition), Theme.SelectionGlow, 4, 8, antialiased: false);
        }

        foreach (var (nodeIndex, position) in _ghostNodePositions)
        {
            if (nodeIndex < 0 || nodeIndex >= _viewModel.Nodes.Count)
            {
                continue;
            }

            var radius = (float)_viewModel.Nodes[nodeIndex].Radius;
            DrawArc(ToGodot(position), radius * 1.35f, 0, Mathf.Tau, 32, Theme.SelectionGlow, 2, antialiased: false);
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
        DrawLine(a.Lerp(center, 0.35f), b.Lerp(center, 0.35f), Theme.SelectionGlow, 2, antialiased: false);
        DrawLine(b.Lerp(center, 0.35f), c.Lerp(center, 0.35f), Theme.SelectionGlow, 2, antialiased: false);
        DrawLine(c.Lerp(center, 0.35f), a.Lerp(center, 0.35f), Theme.SelectionGlow, 2, antialiased: false);

        var labelPosition = center + _rigidLabelOffset;
        DrawRect(new Rect2(labelPosition + _rigidLabelBox.Position, _rigidLabelBox.Size), Theme.ArenaBackground.WithAlpha(0.86f));
        DrawString(ThemeDB.FallbackFont, labelPosition, "Rigid: no joints", HorizontalAlignment.Left, -1, _rigidLabelFontSize, Theme.GroundEdge);
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
        DrawArc(center, radius, arcStart, arcEnd, 28, Theme.MotorAccent, Theme.MotorSignalWidth, antialiased: false);
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
            DrawArc(ToGodot(node.Position), (float)node.Radius * 0.72f, 0, Mathf.Tau, 32, Theme.MotorAccent, Theme.MotorSignalWidth, antialiased: false);
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
            // The beam drag's own rings replace the warning on the joints being joined.
            if (_viewModel.Beams.Any(beam => beam.NodeA == nodeIndex || beam.NodeB == nodeIndex)
                || nodeIndex == _gestures?.BeamStartNode
                || nodeIndex == _gestures?.BeamTargetNode)
            {
                continue;
            }

            var node = _viewModel.Nodes[nodeIndex];
            var position = ToGodot(node.Position);
            var radius = (float)node.Radius * 1.55f;
            DrawArc(position, radius, 0, Mathf.Tau, 32, Theme.Danger, Theme.MotorSignalWidth, antialiased: false);
            DrawLine(position + new Vector2(-radius * 0.45f, -radius * 0.45f), position + new Vector2(radius * 0.45f, radius * 0.45f), Theme.Danger, Theme.MotorSignalWidth, antialiased: false);
            DrawLine(position + new Vector2(radius * 0.45f, -radius * 0.45f), position + new Vector2(-radius * 0.45f, radius * 0.45f), Theme.Danger, Theme.MotorSignalWidth, antialiased: false);
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
            DrawArc(position, radius, 0, Mathf.Tau, 32, Theme.Danger, Theme.MotorSignalWidth, antialiased: false);
            DrawLine(position + new Vector2(-radius * 0.55f, 0), position + new Vector2(radius * 0.55f, 0), Theme.Danger, Theme.MotorSignalWidth, antialiased: false);
            DrawLine(position + new Vector2(0, -radius * 0.55f), position + new Vector2(0, radius * 0.55f), Theme.Danger, Theme.MotorSignalWidth, antialiased: false);
        }
    }

    private void CaptureMoveGhosts(IReadOnlyCollection<int> movingNodes)
    {
        if (_viewModel is null || !_viewModel.IsMoveOnly)
        {
            return;
        }

        _ghostNodePositions.Clear();
        _ghostVersion++;
        foreach (var index in movingNodes)
        {
            if (index >= 0 && index < _viewModel.Nodes.Count)
            {
                _ghostNodePositions[index] = _viewModel.Nodes[index].Position;
            }
        }
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
        tree.CreateTimer(_moveGhostSeconds).Timeout += () =>
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

    private void OnAnatomyChanged(object? sender, EventArgs eventArgs)
    {
        QueueRedraw();
    }

    private void OnGesturesChanged(object? sender, EventArgs eventArgs)
    {
        QueueRedraw();
    }

    private void OnNodeDragStarting(object? sender, IReadOnlyCollection<int> movingNodes)
    {
        CaptureMoveGhosts(movingNodes);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(ConstructionViewModel.SelectedNodeCount))
        {
            QueueRedraw();
        }

        if (eventArgs.PropertyName == nameof(ConstructionViewModel.ActiveTool))
        {
            _gestures?.Cancel();
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
        // to land on the right spot. Mirrors TrainingHost._UnhandledInput's
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
