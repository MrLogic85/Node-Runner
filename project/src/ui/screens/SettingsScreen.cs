using System.ComponentModel;
using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Widgets;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Reference Settings screen: the player's settings for this device.
/// The layout is authored in <c>scenes/screens/SettingsScreen.tscn</c>; this script binds the
/// presentation and forwards Back (the toolbar's, Android Back and Escape) and each choice made.
/// </summary>
public partial class SettingsScreen : Control
{
    private SettingsPresentationViewModel? _presentation;
    private bool _subscribedToPresentation;
    private UiThemeCard[] _themeCards = [];
    private UiSegmentedSwitch _gridSizes = null!;
    private UiToggleRow _snapRow = null!;

    [Signal]
    public delegate void BackRequestedEventHandler();

    [Signal]
    public delegate void ThemePickedEventHandler(ThemeChoice theme);

    [Signal]
    public delegate void GridSizePickedEventHandler(int index);

    [Signal]
    public delegate void SnapToGridToggledEventHandler(bool on);

    public void Setup(SettingsPresentationViewModel presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        UnsubscribeFromPresentation();
        _presentation = presentation;
        if (IsInsideTree())
        {
            SubscribeToPresentation();
        }

        if (IsNodeReady())
        {
            Apply();
        }
    }

    public override void _EnterTree() => SubscribeToPresentation();

    public override void _Ready()
    {
        UiLayout.ApplyScreen(this);
        var toolbar = GetNode<UiToolbar>("%Toolbar");
        toolbar.BackPressed += RequestBack;
        var back = new UiBackHandler { CanTakeBack = () => !toolbar.Menu.Visible };
        back.BackRequested += RequestBack;
        AddChild(back, @internal: InternalMode.Front);
        _themeCards = [GetNode<UiThemeCard>("%NeonCard"), GetNode<UiThemeCard>("%PaperCard"), GetNode<UiThemeCard>("%PhoneCard")];
        foreach (var card in _themeCards)
        {
            card.Picked += () => EmitSignal(SignalName.ThemePicked, (int)ThemeChoices.FromUi(card.Choice));
        }

        _gridSizes = GetNode<UiSegmentedSwitch>("%GridSizes");
        _gridSizes.Segments = [.. BuildGridSizes.Labels.Select(label => new UiSegment { TextSource = UiTextTranslation.Source(label) })];
        // Apply after each choice puts the controls back when it could not be saved.
        _gridSizes.SelectionChanged += index =>
        {
            EmitSignal(SignalName.GridSizePicked, index);
            Apply();
        };
        _snapRow = GetNode<UiToggleRow>("%SnapRow");
        _snapRow.Toggled += on =>
        {
            EmitSignal(SignalName.SnapToGridToggled, on);
            Apply();
        };

        Apply();
    }

    public override void _ExitTree() => UnsubscribeFromPresentation();

    private void RequestBack() => EmitSignal(SignalName.BackRequested);

    private void SubscribeToPresentation()
    {
        if (_presentation is null || _subscribedToPresentation)
        {
            return;
        }

        _presentation.PropertyChanged += OnPresentationChanged;
        _subscribedToPresentation = true;
    }

    private void UnsubscribeFromPresentation()
    {
        if (_presentation is null || !_subscribedToPresentation)
        {
            return;
        }

        _presentation.PropertyChanged -= OnPresentationChanged;
        _subscribedToPresentation = false;
    }

    private void OnPresentationChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (IsNodeReady())
        {
            Apply();
        }
    }

    private void Apply()
    {
        // Each card's scene-authored Choice decides which theme it picks and shows.
        var theme = _presentation?.Theme;
        foreach (var card in _themeCards)
        {
            card.Chosen = ThemeChoices.FromUi(card.Choice) == theme;
        }

        if (_presentation is not null)
        {
            _gridSizes.SelectedIndex = _presentation.GridSizeIndex;
            _snapRow.On = _presentation.SnapToGrid;
        }
    }
}
