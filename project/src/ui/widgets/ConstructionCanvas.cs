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
/// or drag nodes, connect two nodes with a beam, or attach/remove a core.
/// Binds to <see cref="ConstructionViewModel"/> per `project/src/ui/AGENTS.md`;
/// does not own any anatomy state itself. See docs/CONSTRUCTION_MODE.md.
/// </summary>
public partial class ConstructionCanvas : Node2D
{
    private const float _nodeHitRadius = 32f;
    private const float _defaultNodeRadius = 18f;

    private ConstructionViewModel? _viewModel;
    private int _draggingNodeIndex = -1;

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
            if (_draggingNodeIndex >= 0)
            {
                HandleDrag(ToCanvasLocal(dragPosition));
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (PointerInput.TryGetReleasePosition(inputEvent, out _))
        {
            _draggingNodeIndex = -1;
        }
    }

    public override void _Draw()
    {
        if (_viewModel is null)
        {
            return;
        }

        foreach (var beam in _viewModel.Beams)
        {
            var start = ToGodot(_viewModel.Nodes[beam.NodeA].Position);
            var end = ToGodot(_viewModel.Nodes[beam.NodeB].Position);
            DrawLine(start, end, Theme.Beam, Theme.BeamWidth, antialiased: true);
        }

        foreach (var node in _viewModel.Nodes)
        {
            var position = ToGodot(node.Position);
            DrawCircle(position, (float)node.Radius * 1.18f, Theme.NodeGlow);
            DrawCircle(position, (float)node.Radius, Theme.NodeFill);
        }

        foreach (var core in _viewModel.Cores)
        {
            var position = ToGodot(_viewModel.Nodes[core.NodeIndex].Position);
            var radius = (float)_viewModel.Nodes[core.NodeIndex].Radius;
            DrawCircle(position, radius * 0.42f, Theme.CoreMarker);
        }

        if (_viewModel.PendingBeamStartNode is { } pendingIndex)
        {
            var position = ToGodot(_viewModel.Nodes[pendingIndex].Position);
            var radius = (float)_viewModel.Nodes[pendingIndex].Radius;
            DrawCircle(position, radius * 1.65f, Theme.SelectionGlow);
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
            case ConstructionTool.Place:
            default:
                if (foundNode)
                {
                    _draggingNodeIndex = nodeIndex;
                }
                else
                {
                    _draggingNodeIndex = _viewModel.PlaceNode(domainPosition, _defaultNodeRadius);
                }

                break;
        }
    }

    private void HandleDrag(Vector2 localPosition)
    {
        if (_viewModel is null || _viewModel.ActiveTool != ConstructionTool.Place)
        {
            return;
        }

        if (_draggingNodeIndex < 0 || _draggingNodeIndex >= _viewModel.Nodes.Count)
        {
            return;
        }

        _viewModel.MoveNode(_draggingNodeIndex, ToDomain(localPosition));
    }

    private void OnAnatomyChanged(object? sender, EventArgs eventArgs)
    {
        QueueRedraw();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(ConstructionViewModel.PendingBeamStartNode))
        {
            QueueRedraw();
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
