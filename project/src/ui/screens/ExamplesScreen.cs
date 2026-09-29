using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Widgets;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Reference Examples screen: ready-made creations the player copies into Creations. The layout is
/// authored in <c>scenes/screens/ExamplesScreen.tscn</c>; this script binds the presentation and
/// forwards Back (the toolbar's, Android Back and Escape) and Copy.
/// </summary>
public partial class ExamplesScreen : Control
{
    private ExamplesPresentationViewModel? _presentation;

    [Signal]
    public delegate void BackRequestedEventHandler();

    [Signal]
    public delegate void CopyRequestedEventHandler(string exampleKey, string exampleName);

    /// <summary>The Creations card, instanced once per example.</summary>
    [Export]
    public PackedScene? CardScene { get; set; }

    public void Setup(ExamplesPresentationViewModel presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        _presentation = presentation;
        if (IsNodeReady())
        {
            Apply();
        }
    }

    public override void _Ready()
    {
        UiLayout.ApplyScreen(this);
        var toolbar = GetNode<UiToolbar>("%Toolbar");
        toolbar.BackPressed += RequestBack;
        var back = new UiBackHandler { CanTakeBack = () => !toolbar.Menu.Visible };
        back.BackRequested += RequestBack;
        AddChild(back, @internal: InternalMode.Front);
        Apply();
    }

    private void RequestBack() => EmitSignal(SignalName.BackRequested);

    private void Apply()
    {
        var row = GetNode<Control>("%CardRow");
        foreach (var card in row.GetChildren().OfType<CreationCard>())
        {
            row.RemoveChild(card);
            card.QueueFree();
        }

        if (_presentation is null || CardScene is null)
        {
            return;
        }

        foreach (var example in _presentation.Cards)
        {
            var card = CardScene.Instantiate<CreationCard>();
            card.Bind(example);
            card.DuplicateRequested += OnCopyRequested;
            row.AddChild(card);
            UiNativeScroll.AllowGesturesToBubble(card);
        }
    }

    private void OnCopyRequested(string exampleKey, string exampleName)
    {
        if (Guid.TryParse(exampleKey, out var id) && _presentation?.RequestCopy(id) is { } exampleId)
        {
            EmitSignal(SignalName.CopyRequested, exampleId.ToString("D"), exampleName);
        }
    }
}
