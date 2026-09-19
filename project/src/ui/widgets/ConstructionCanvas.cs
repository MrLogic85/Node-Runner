using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// Renders the nodes placed so far in construction mode and lets the user
/// place new ones or drag existing ones with touch. Binds to
/// <see cref="ConstructionViewModel"/> per `project/src/ui/AGENTS.md`; does
/// not own any anatomy state itself. See docs/ROADMAP.md 0.3.0.
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
                _viewModel.NodesChanged -= OnNodesChanged;
            }

            _viewModel = value;

            if (_viewModel is not null)
            {
                _viewModel.NodesChanged += OnNodesChanged;
            }

            QueueRedraw();
        }
    }

    public override void _ExitTree()
    {
        if (_viewModel is not null)
        {
            _viewModel.NodesChanged -= OnNodesChanged;
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
            HandlePress(ToLocal(pressPosition));
            GetViewport().SetInputAsHandled();
            return;
        }

        if (PointerInput.TryGetDragPosition(inputEvent, out var dragPosition))
        {
            if (_draggingNodeIndex >= 0)
            {
                HandleDrag(ToLocal(dragPosition));
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

        foreach (var node in _viewModel.Nodes)
        {
            var position = ToGodot(node.Position);
            DrawCircle(position, (float)node.Radius * 1.18f, Theme.NodeGlow);
            DrawCircle(position, (float)node.Radius, Theme.NodeFill);
        }
    }

    private void HandlePress(Vector2 localPosition)
    {
        if (_viewModel is null)
        {
            return;
        }

        var domainPosition = ToDomain(localPosition);
        if (_viewModel.TryFindNodeNear(domainPosition, _nodeHitRadius, out var nodeIndex))
        {
            _draggingNodeIndex = nodeIndex;
            return;
        }

        _draggingNodeIndex = _viewModel.PlaceNode(domainPosition, _defaultNodeRadius);
    }

    private void HandleDrag(Vector2 localPosition)
    {
        if (_viewModel is null || _draggingNodeIndex < 0 || _draggingNodeIndex >= _viewModel.Nodes.Count)
        {
            return;
        }

        _viewModel.MoveNode(_draggingNodeIndex, ToDomain(localPosition));
    }

    private void OnNodesChanged(object? sender, EventArgs eventArgs)
    {
        QueueRedraw();
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
