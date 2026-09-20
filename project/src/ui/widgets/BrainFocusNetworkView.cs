using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

public partial class BrainFocusNetworkView : Control
{
    private readonly Dictionary<(int Layer, int Neuron), Vector2> _positions = new();
    private BrainFocusPresentationViewModel? _viewModel;
    private UiTokens _tokens = UiTokens.Neon;

    public BrainFocusPresentationViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= OnViewModelChanged;
            }

            _viewModel = value;
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged += OnViewModelChanged;
            }

            QueueRedraw();
        }
    }

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            QueueRedraw();
        }
    }

    public override void _ExitTree()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelChanged;
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_viewModel is null || !_viewModel.HasNetwork)
        {
            return;
        }

        if (@event is InputEventScreenTouch { Pressed: true } touch)
        {
            SelectNearestNeuron(touch.Position);
        }
        else if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse)
        {
            SelectNearestNeuron(mouse.Position);
        }
    }

    public override void _Draw()
    {
        _positions.Clear();
        if (_viewModel is null || !_viewModel.HasNetwork || _viewModel.Layers.Count == 0)
        {
            DrawWaitingState();
            return;
        }

        CacheNeuronPositions();
        foreach (var edge in _viewModel.Edges)
        {
            if (edge.Strength < 0.06)
            {
                continue;
            }

            if (!_positions.TryGetValue((edge.FromLayerIndex, edge.FromNeuronIndex), out var from) ||
                !_positions.TryGetValue((edge.ToLayerIndex, edge.ToNeuronIndex), out var to))
            {
                continue;
            }

            var color = edge.Weight >= 0 ? _tokens.LineStrong : _tokens.Danger;
            color.A = (float)Math.Clamp(0.06 + (edge.Strength * 0.58), 0.06, 0.64);
            DrawEdge(from, to, color, (float)(0.5 + edge.Strength * 3.5), edge.Weight < 0);
        }

        foreach (var layer in _viewModel.Layers)
        {
            foreach (var neuron in layer.Neurons)
            {
                var position = _positions[(neuron.LayerIndex, neuron.Index)];
                var selected = neuron.LayerIndex == _viewModel.SelectedLayerIndex &&
                    neuron.Index == _viewModel.SelectedNeuronIndex;
                DrawNeuron(position, neuron, selected);
            }
        }
    }

    private void DrawWaitingState()
    {
        var style = new StyleBoxFlat
        {
            BgColor = _tokens.Panel,
            BorderColor = _tokens.Line,
            BorderWidthLeft = (int)_tokens.StrokeHair,
            BorderWidthTop = (int)_tokens.StrokeHair,
            BorderWidthRight = (int)_tokens.StrokeHair,
            BorderWidthBottom = (int)_tokens.StrokeHair,
            CornerRadiusTopLeft = (int)_tokens.RadiusLarge,
            CornerRadiusTopRight = (int)_tokens.RadiusLarge,
            CornerRadiusBottomLeft = (int)_tokens.RadiusLarge,
            CornerRadiusBottomRight = (int)_tokens.RadiusLarge,
        };
        DrawStyleBox(style, new Rect2(Vector2.Zero, Size));
        DrawCircle(Size / 2, 14, _tokens.LineStrong);
    }

    private void CacheNeuronPositions()
    {
        var layers = _viewModel!.Layers;
        var left = 42f;
        var right = Math.Max(left + 1, Size.X - 42f);
        var top = 26f;
        var bottom = Math.Max(top + 1, Size.Y - 26f);

        for (var layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            var layer = layers[layerIndex];
            var x = layers.Count == 1
                ? Size.X / 2
                : Mathf.Lerp(left, right, (float)layerIndex / (layers.Count - 1));
            for (var neuronIndex = 0; neuronIndex < layer.Neurons.Count; neuronIndex++)
            {
                var y = layer.Neurons.Count == 1
                    ? Size.Y / 2
                    : Mathf.Lerp(top, bottom, (float)neuronIndex / (layer.Neurons.Count - 1));
                _positions[(layerIndex, neuronIndex)] = new Vector2(x, y);
            }
        }
    }

    private void DrawNeuron(Vector2 position, BrainFocusNeuronPresentation neuron, bool selected)
    {
        var baseColor = neuron.Activation >= 0 ? _tokens.LineStrong : _tokens.Danger;
        baseColor.A = (float)Math.Clamp(0.30 + (neuron.ActivationFill * 0.70), 0.30, 1);
        var radius = 8 + (float)(neuron.ActivationFill * 8);
        DrawCircle(position, radius + 3, _tokens.PanelRaised);
        if (neuron.Activation >= 0)
        {
            DrawCircle(position, radius, baseColor);
        }
        else
        {
            DrawColoredPolygon(
                [
                    position + new Vector2(0, -radius),
                    position + new Vector2(radius, 0),
                    position + new Vector2(0, radius),
                    position + new Vector2(-radius, 0),
                ],
                baseColor);
        }

        if (selected)
        {
            DrawArc(position, radius + 8, 0, Mathf.Tau, 40, _tokens.Halo, 3, antialiased: true);
        }
    }

    private void DrawEdge(Vector2 from, Vector2 to, Color color, float width, bool dashed)
    {
        if (!dashed)
        {
            DrawLine(from, to, color, width, antialiased: true);
            return;
        }

        var delta = to - from;
        var length = delta.Length();
        if (length <= 0.001f)
        {
            return;
        }

        var direction = delta / length;
        const float dashLength = 10f;
        const float gapLength = 7f;
        for (var offset = 0f; offset < length; offset += dashLength + gapLength)
        {
            var start = from + direction * offset;
            var end = from + direction * Math.Min(offset + dashLength, length);
            DrawLine(start, end, color, width, antialiased: true);
        }
    }

    private void SelectNearestNeuron(Vector2 position)
    {
        var bestDistanceSquared = 24f * 24f;
        (int Layer, int Neuron)? best = null;
        foreach (var candidate in _positions)
        {
            var distanceSquared = candidate.Value.DistanceSquaredTo(position);
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                best = candidate.Key;
            }
        }

        if (best is { } selected)
        {
            _viewModel!.SelectNeuron(selected.Layer, selected.Neuron);
        }
    }

    private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        QueueRedraw();
    }
}
