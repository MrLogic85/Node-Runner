using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The Build screen's Brain setup page (<c>reference design/components/BrainSetup</c>): hidden
/// layers, the neurons every hidden layer shares, and the brain they make. The scene owns the
/// layout; this binds it to <see cref="BrainSetupPresentation"/> and reports each new shape.
/// </summary>
public partial class BrainSetupSheet : Control
{
    private ConstructionPresentationViewModel? _presentation;

    [Signal]
    public delegate void BrainShapeChangedEventHandler(int hiddenLayers, int neuronsPerLayer);

    public bool IsOpen => Visible;

    public ConstructionPresentationViewModel? Presentation
    {
        get => _presentation;
        set
        {
            if (_presentation is not null)
            {
                _presentation.PresentationChanged -= OnPresentationChanged;
            }

            _presentation = value;
            if (_presentation is not null && IsInsideTree())
            {
                _presentation.PresentationChanged += OnPresentationChanged;
            }

            Refresh();
        }
    }

    private BrainSetupPresentation? Setup => _presentation?.BrainSetup;

    public override void _EnterTree()
    {
        if (_presentation is not null)
        {
            _presentation.PresentationChanged += OnPresentationChanged;
        }
    }

    public override void _ExitTree()
    {
        if (_presentation is not null)
        {
            _presentation.PresentationChanged -= OnPresentationChanged;
        }
    }

    public override void _Ready()
    {
        GetNode<UiToolbar>("%Toolbar").BackPressed += Close;
        GetNode<UiButton>("%UseDefaults").Activated += () => Emit(Setup?.Defaults);
        GetNode<UiSegmentedSwitch>("%Layers").SelectionChanged += index => Emit(Setup?.WithHiddenLayers(index + 1));
        GetNode<UiButton>("%Fewer").Activated += () => Emit(Setup?.WithNeuronsStepped(-1));
        GetNode<UiButton>("%More").Activated += () => Emit(Setup?.WithNeuronsStepped(1));
        GetNode<UiSlider>("%Neurons").ThumbChangeCommitted += (_, position) => Emit(Setup?.WithNeuronsAt(position));
        Refresh();
    }

    public void Open()
    {
        Refresh();
        Show();
    }

    public void Close() => Hide();

    // A hidden page skips the work; Open refreshes it before it shows.
    private void OnPresentationChanged(object? sender, EventArgs eventArgs)
    {
        if (Visible)
        {
            Refresh();
        }
    }

    private void Emit(BrainShapeDef? shape)
    {
        if (shape is not null)
        {
            EmitSignal(SignalName.BrainShapeChanged, shape.HiddenLayers, shape.NeuronsPerLayer);
        }
    }

    private void Refresh()
    {
        if (_presentation is null || !IsNodeReady())
        {
            return;
        }

        var setup = _presentation.BrainSetup;
        GetNode<Label>("%CreationName").Text = _presentation.CreationName;
        GetNode<UiSegmentedSwitch>("%Layers").SelectedIndex = setup.Shape.HiddenLayers - 1;
        GetNode<Control>("%Recommended").Visible = setup.IsRecommended;
        GetNode<Control>("%NotRecommended").Visible = !setup.IsRecommended;
        GetNode<Label>("%RecommendedText").Text = setup.Advice;
        GetNode<Label>("%NotRecommendedText").Text = setup.Advice;

        GetNode<Label>("%LayersLabel").Text = setup.LayersLabel;
        GetNode<Label>("%NeuronsReadout").Text = setup.Shape.NeuronsPerLayer.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var neurons = GetNode<UiSlider>("%Neurons");
        neurons.Value = UiSliderValue.Thumb(setup.NeuronsPosition);
        neurons.MarkerPosition = setup.DefaultPosition;
        neurons.MarkerText = setup.DefaultText;

        GetNode<Control>("%NoAnatomy").Visible = !setup.HasAnatomy;
        GetNode<Control>("%Preview").Visible = setup.HasAnatomy;
        GetNode<BrainSetupNetwork>("%Network").Setup = setup;
        GetNode<Label>("%Connections").Text = setup.Connections.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ShowColumns(setup);
    }

    // Columns follow the layers: senses, one per hidden layer, then outputs.
    private void ShowColumns(BrainSetupPresentation setup)
    {
        var slots = ColumnSlots();
        var outputsSlot = slots.Length - 1;
        var hiddenLayers = setup.Shape.HiddenLayers;
        for (var slot = 0; slot < slots.Length; slot++)
        {
            var visible = slot == outputsSlot || slot <= hiddenLayers;
            var column = slot == outputsSlot ? hiddenLayers + 1 : slot;
            slots[slot].Show(visible && column < setup.Columns.Count ? setup.Columns[column] : null);
        }
    }

    private ColumnSlot[] ColumnSlots() =>
    [
        new(GetNode<Label>("%SensesHeader"), GetNode<Label>("%SensesCount"), GetNode<Label>("%SensesMore")),
        new(GetNode<Label>("%Hidden1Header"), GetNode<Label>("%Hidden1Count"), GetNode<Label>("%Hidden1More")),
        new(GetNode<Label>("%Hidden2Header"), GetNode<Label>("%Hidden2Count"), GetNode<Label>("%Hidden2More")),
        new(GetNode<Label>("%Hidden3Header"), GetNode<Label>("%Hidden3Count"), GetNode<Label>("%Hidden3More")),
        new(GetNode<Label>("%OutputsHeader"), GetNode<Label>("%OutputsCount"), GetNode<Label>("%OutputsMore")),
    ];

    /// <summary>One column's header, neuron count and "+N" caption; hidden when its layer is not in the brain.</summary>
    private readonly record struct ColumnSlot(Label Header, Label Count, Label More)
    {
        public void Show(BrainSetupColumn? column)
        {
            Header.Visible = Count.Visible = More.Visible = column is not null;
            if (column is not null)
            {
                Count.Text = column.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                More.Text = column.MoreText;
            }
        }
    }
}
