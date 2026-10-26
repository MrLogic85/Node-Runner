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
/// presentation and forwards Back (the toolbar's, Android Back and Escape) and the theme picked.
/// </summary>
public partial class SettingsScreen : Control
{
    private SettingsPresentationViewModel? _presentation;
    private bool _subscribedToPresentation;
    private UiThemeCard[] _themeCards = [];

    [Signal]
    public delegate void BackRequestedEventHandler();

    [Signal]
    public delegate void ThemePickedEventHandler(ThemeChoice theme);

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
    }
}
