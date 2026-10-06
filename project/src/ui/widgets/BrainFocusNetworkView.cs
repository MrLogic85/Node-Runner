using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// Draws the live direct brain: labelled inputs and outputs under their column headings, weighted connections and the selected neuron.
/// With a selection, connections that don't touch it fade and its strongest partners light up. Neurons shrink to fit tall columns, and
/// every row keeps its label: when the rows would get closer than a label line, the network scrolls under its pinned headings
/// (<see cref="BrainFocusRowLayout"/>, #908). The rows sit in the scene's ScrollContainer, which owns the drag, fling and wheel;
/// a tap on a row selects on release unless the drag scrolled, as the creation cards do.
/// </summary>
public partial class BrainFocusNetworkView : Control
{
    private const float _sideInset = 12f;
    private const float _labelGap = 10f;
    private const float _maxLabelShare = 0.4f;
    private const float _haloGap = BrainFocusRowLayout.HaloGap;
    private const float _haloWidth = BrainFocusRowLayout.HaloWidth;
    private const float _selectedEdgeMinAlpha = 0.4f;
    private const float _selectedEdgeMinWidth = 1.5f;
    private const float _tapRadius = 24f;
    private const UiTokens.Typography _labelStyle = UiTokens.Typography.Caption;
    private const UiTokens.Typography _headingStyle = UiTokens.Typography.Overline;

    private readonly Dictionary<(int Layer, int Neuron), Vector2> _positions = new();
    private float _radius = BrainFocusRowLayout.MaxRadius;
    private bool _tapPending;
    private BrainFocusPresentationViewModel? _viewModel;

    // The rows, edges and dots draw in Rows, inside the scene's Scroll. Scroll starts a gap below the
    // headings and clips only while the rows overflow, so scrolled rows are cut before the headings.
    private ScrollContainer? _scroll;
    private BrainFocusRows? _body;
    private float _clipTop;

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

            UpdateRows();
            Redraw();
        }
    }

    public override void _Ready()
    {
        _scroll = GetNode<ScrollContainer>("Scroll");
        _clipTop = _scroll.OffsetTop;
        _scroll.ScrollStarted += () => _tapPending = false;
        _body = GetNode<BrainFocusRows>("Scroll/Rows");
        _body.Draw += DrawBody;
        _body.GuiInput += OnRowsInput;
        Resized += UpdateRows;
        UpdateRows();
    }

    public override void _ExitTree()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelChanged;
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged || what == NotificationTranslationChanged)
        {
            UpdateRows();
            Redraw();
        }
    }

    // A tap on the headings, above the rows, clears the selection.
    public override void _GuiInput(InputEvent @event)
    {
        if (_viewModel is { HasNetwork: true } && PointerInput.TryGetPressPosition(@event, out _))
        {
            _viewModel.ClearSelection();
            AcceptEvent();
        }
    }

    // Unaccepted, so the ScrollContainer still sees the drag; its ScrollStarted cancels the tap.
    private void OnRowsInput(InputEvent @event)
    {
        if (_viewModel is null || !_viewModel.HasNetwork)
        {
            return;
        }

        if (PointerInput.TryGetPressPosition(@event, out _))
        {
            _tapPending = true;
        }
        else if (PointerInput.TryGetReleasePosition(@event, out var release) && _tapPending)
        {
            _tapPending = false;
            SelectNearestNeuron(release + new Vector2(0, _clipTop));
        }
    }

    // Sizes the rows for the ScrollContainer and clips only while they overflow, as UiToolbar's field does.
    private void UpdateRows()
    {
        if (_body is null || _scroll is null)
        {
            return;
        }

        var rows = RowLayout();
        _body.ContentHeight = rows is { Scrolls: true } scrolled ? scrolled.ContentHeight - _clipTop : 0;
        _scroll.ClipContents = rows?.Scrolls == true;
    }

    private BrainFocusRowLayout? RowLayout() =>
        _viewModel is { HasNetwork: true, Layers.Count: > 0 }
            ? BrainFocusRowLayout.For(Size.Y, _viewModel.Layers.Max(layer => layer.Neurons.Count), UiThemeLookup.FontSize(this, _labelStyle), _clipTop)
            : null;

    private void Redraw()
    {
        QueueRedraw();
        _body?.QueueRedraw();
    }

    // The view draws the headings and the waiting state; its body draws the network.
    public override void _Draw()
    {
        if (Layout() is not { } layout)
        {
            DrawWaitingState();
            return;
        }

        DrawHeadings(layout.Texts, layout.HeadingFont, layout.HeadingSize, layout.LabelWidths);
    }

    private void DrawBody()
    {
        if (_body is null || Layout() is not { } layout)
        {
            return;
        }

        var toBody = Transform2D.Identity.Translated(new Vector2(0, -_clipTop));
        _body.DrawSetTransformMatrix(toBody);
        DrawNetwork(_body, toBody);
        DrawLabels(_body, layout.Texts, layout.Font, layout.FontSize, layout.LabelWidths);
        _body.DrawSetTransformMatrix(Transform2D.Identity);
    }

    private sealed record DrawLayout(ColumnText[] Texts, Font Font, int FontSize, Font HeadingFont, int HeadingSize, (float Input, float Output) LabelWidths);

    // Measures the labels and places every row in view coordinates, before scrolling. Both draws call
    // it, so neither depends on the other drawing first.
    private DrawLayout? Layout()
    {
        _positions.Clear();
        if (_viewModel is null || !_viewModel.HasNetwork || _viewModel.Layers.Count == 0)
        {
            return null;
        }

        var font = GetThemeFont("font", UiTokens.Variation(_labelStyle));
        var fontSize = UiThemeLookup.FontSize(this, _labelStyle);
        var headingFont = GetThemeFont("font", UiTokens.Variation(_headingStyle));
        var headingSize = UiThemeLookup.FontSize(this, _headingStyle);
        var texts = InLanguage(_viewModel.Layers);
        var labelWidths = LabelColumnWidths(texts, font, fontSize, headingFont, headingSize);
        CacheNeuronPositions(labelWidths);
        return new DrawLayout(texts, font, fontSize, headingFont, headingSize, labelWidths);
    }

    // Each column's heading and neuron labels in the player's language, translated once per draw
    // and used both to measure and to draw.
    private static ColumnText[] InLanguage(IReadOnlyList<BrainFocusLayerPresentation> layers) =>
        [.. layers.Select(layer => new ColumnText(
            Heading(UiTextTranslation.Source(layer.Title)()),
            [.. layer.Neurons.Select(neuron => UiTextTranslation.Source(neuron.Label)())]))];

    private sealed record ColumnText(string Heading, string[] Labels);

    // In window pixels, so the edges, dots and halo are smooth at any UI size (#733).
    private void DrawNetwork(CanvasItem canvas, Transform2D drawTransform)
    {
        using var pen = UiPixelPen.Begin(canvas, drawTransform);
        foreach (var edge in _viewModel!.Edges)
        {
            var faded = _viewModel.Selected is not null && !edge.IsHighlighted;
            if (edge.Strength < 0.06 && !edge.IsHighlighted)
            {
                continue;
            }

            if (!_positions.TryGetValue((edge.FromLayerIndex, edge.FromNeuronIndex), out var from) ||
                !_positions.TryGetValue((edge.ToLayerIndex, edge.ToNeuronIndex), out var to))
            {
                continue;
            }

            var color = edge.Weight >= 0 ? UiThemeLookup.Color(this, UiTokens.Color.LineStrong) : UiThemeLookup.Color(this, UiTokens.Color.Danger);
            var alpha = faded ? 0.06f : (float)Math.Clamp(0.06 + (edge.Strength * 0.58), 0.06, 0.64);
            var width = (float)(0.5 + edge.Strength * 3.5);
            if (_viewModel.Selected is not null && edge.IsHighlighted)
            {
                alpha = Math.Max(alpha, _selectedEdgeMinAlpha);
                width = Math.Max(width, _selectedEdgeMinWidth);
            }

            color.A = alpha;
            DrawEdge(pen, from, to, color, width, edge.Weight < 0);
        }

        foreach (var layer in _viewModel.Layers)
        {
            foreach (var neuron in layer.Neurons)
            {
                DrawNeuron(pen, _positions[(neuron.LayerIndex, neuron.Index)], neuron);
            }
        }
    }

    private void DrawWaitingState()
    {
        using var pen = UiPixelPen.Begin(this);
        pen.Disc(Size / 2, 14, UiThemeLookup.Color(this, UiTokens.Color.LineStrong));
    }

    // Each column is as wide as its widest label or its heading, whichever is wider.
    private (float Input, float Output) LabelColumnWidths(ColumnText[] texts, Font font, int fontSize, Font headingFont, int headingSize)
    {
        var cap = Size.X * _maxLabelShare;
        return (Widest(texts[0]), texts.Length > 1 ? Widest(texts[^1]) : 0);

        float Widest(ColumnText column) =>
            column.Labels.Length == 0 ? 0 : Math.Min(
                cap,
                column.Labels
                    .Select(label => font.GetStringSize(label, fontSize: fontSize).X)
                    .Append(headingFont.GetStringSize(column.Heading, fontSize: headingSize).X)
                    .Max());
    }

    private static string Heading(string title) => UiThemeLookup.LetterCase(title, _headingStyle);

    private void CacheNeuronPositions((float Input, float Output) labelWidths)
    {
        var layers = _viewModel!.Layers;
        var tallest = layers.Max(layer => layer.Neurons.Count);
        var rows = RowLayout()!.Value;
        _radius = rows.Radius;
        var left = _sideInset + labelWidths.Input + _labelGap + _radius;
        var right = Math.Max(left + 1, Size.X - _sideInset - labelWidths.Output - _labelGap - _radius);
        for (var layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            var layer = layers[layerIndex];
            var x = layers.Count == 1
                ? Size.X / 2
                : Mathf.Lerp(left, right, (float)layerIndex / (layers.Count - 1));
            for (var neuronIndex = 0; neuronIndex < layer.Neurons.Count; neuronIndex++)
            {
                var y = rows.RowY(neuronIndex, layer.Neurons.Count, tallest);
                _positions[(layerIndex, neuronIndex)] = new Vector2(x, y);
            }
        }
    }

    // Every row keeps its label in both columns (#908); the layout keeps rows a label line apart.
    private void DrawLabels(CanvasItem canvas, ColumnText[] texts, Font font, int fontSize, (float Input, float Output) labelWidths)
    {
        var layers = _viewModel!.Layers;
        var baselineOffset = (font.GetAscent(fontSize) - font.GetDescent(fontSize)) / 2;
        for (var layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            var neurons = layers[layerIndex].Neurons;
            var isInput = layerIndex == 0;
            var width = isInput ? labelWidths.Input : labelWidths.Output;
            foreach (var neuron in neurons)
            {
                var position = _positions[(neuron.LayerIndex, neuron.Index)];
                var x = isInput
                    ? position.X - _radius - _labelGap - width
                    : position.X + _radius + _labelGap;
                var color = UiThemeLookup.Color(this, neuron.IsHighlighted ? UiTokens.Color.Ink : UiTokens.Color.Muted);
                canvas.DrawString(
                    font,
                    new Vector2(x, position.Y + baselineOffset),
                    texts[layerIndex].Labels[neuron.Index],
                    isInput ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                    width,
                    fontSize,
                    color);
            }
        }
    }

    // Each heading sits over its label column, flush with the labels' inner edge like a table header,
    // so a large first dot never runs into it.
    private void DrawHeadings(ColumnText[] texts, Font font, int fontSize, (float Input, float Output) labelWidths)
    {
        var layers = _viewModel!.Layers;
        var color = UiThemeLookup.Color(this, UiTokens.Color.Muted);
        for (var layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            var layer = layers[layerIndex];
            if (layer.Neurons.Count == 0)
            {
                continue;
            }

            var isInput = layerIndex == 0;
            var width = isInput ? labelWidths.Input : labelWidths.Output;
            var columnX = _positions[(layerIndex, 0)].X;
            var x = isInput ? columnX - _radius - _labelGap - width : columnX + _radius + _labelGap;
            DrawString(
                font,
                new Vector2(x, font.GetAscent(fontSize)),
                texts[layerIndex].Heading,
                isInput ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                width,
                fontSize,
                color);
        }
    }

    private void DrawNeuron(UiPixelPen pen, Vector2 position, BrainFocusNeuronPresentation neuron)
    {
        var baseColor = neuron.Activation >= 0 ? UiThemeLookup.Color(this, UiTokens.Color.LineStrong) : UiThemeLookup.Color(this, UiTokens.Color.Danger);
        baseColor.A = neuron.IsHighlighted ? 1 : (float)Math.Clamp(0.30 + (neuron.ActivationFill * 0.70), 0.30, 1);
        var radius = (_radius / 2) + (float)(neuron.ActivationFill * _radius / 2);
        pen.Disc(position, radius + 2, UiThemeLookup.Color(this, UiTokens.Color.PanelRaised));
        if (neuron.Activation >= 0)
        {
            pen.Disc(position, radius, baseColor);
        }
        else
        {
            // A diamond is a square on its corner: one wide line along a diagonal, so its edges are antialiased.
            var halfDiagonal = new Vector2(radius, radius) / 2;
            pen.Line(position - halfDiagonal, position + halfDiagonal, baseColor, radius * Mathf.Sqrt2);
        }

        if (neuron.IsSelected)
        {
            pen.Ring(position, _radius + _haloGap, UiThemeLookup.Color(this, UiTokens.Color.Halo), _haloWidth, 40);
        }
    }

    private static void DrawEdge(UiPixelPen pen, Vector2 from, Vector2 to, Color color, float width, bool dashed)
    {
        if (!dashed)
        {
            pen.Line(from, to, color, width);
            return;
        }

        pen.DashedLine(from, to, color, width, dash: 10, gap: 7);
    }

    // Each row is a band across its label and dot, so tapping the name works; a tap outside every row
    // clears. The position is in view coordinates before scrolling. A row scrolled under the headings
    // or out of the card can't be tapped.
    private void SelectNearestNeuron(Vector2 position)
    {
        var layers = _viewModel!.Layers;
        var scroll = _scroll?.ScrollVertical ?? 0;
        var column = -1;
        for (var layerIndex = 0; layerIndex < layers.Count && column < 0; layerIndex++)
        {
            if (layers[layerIndex].Neurons.Count == 0)
            {
                continue;
            }

            var x = _positions[(layerIndex, 0)].X;
            var isFirst = layerIndex == 0;
            var isLast = layerIndex == layers.Count - 1;
            if ((isFirst && position.X <= x + _tapRadius) || (isLast && position.X >= x - _tapRadius))
            {
                column = layerIndex;
            }
        }

        var bestDistance = _tapRadius;
        int? best = null;
        if (column >= 0)
        {
            foreach (var neuron in layers[column].Neurons)
            {
                var y = _positions[(column, neuron.Index)].Y;
                var distance = Math.Abs(y - position.Y);
                if (y - scroll >= _clipTop && y - scroll <= Size.Y && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = neuron.Index;
                }
            }
        }

        if (best is { } index)
        {
            _viewModel.SelectNeuron(column, index);
        }
        else
        {
            _viewModel.ClearSelection();
        }
    }

    private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        UpdateRows();
        Redraw();
    }
}
