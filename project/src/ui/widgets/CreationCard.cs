using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// One creation card: a saved creation on Creations, or a ready-made one on Examples, where the
/// presentation leaves out Open and Delete. A trained creation shows a padlock, its best run's
/// values, the map and its generations; otherwise the summary line stands under the name. The layout is authored in
/// <c>scenes/widgets/CreationCard.tscn</c>; this script binds a <see cref="CreationCardPresentation"/>
/// and forwards the card's actions.
/// </summary>
public partial class CreationCard : MarginContainer
{
    [Signal]
    public delegate void OpenRequestedEventHandler(string creationKey, string creationName);

    [Signal]
    public delegate void DuplicateRequestedEventHandler(string creationKey, string creationName);

    [Signal]
    public delegate void DeleteRequestedEventHandler(string creationKey, string creationName);

    private CreationCardPresentation? _creation;
    private Control _actions = null!;
    private UiCard _frame = null!;
    private Control _pressOverlay = null!;
    private bool _openPending;
    private bool _pressShown;

    public void Bind(CreationCardPresentation creation)
    {
        _creation = creation;
        if (IsNodeReady())
        {
            Apply();
        }
    }

    public override void _Ready()
    {
        _actions = GetNode<Control>("%Actions");
        _frame = GetNode<UiCard>("Frame");
        // The frame's last child, so the tint lies over the thumbnail's own background.
        _pressOverlay = GetNode<Control>("%PressOverlay");
        _pressOverlay.Draw += DrawPress;
        GetNode<UiButton>("%Copy").Activated += () => Emit(SignalName.DuplicateRequested);
        GetNode<UiButton>("%Delete").Activated += () => Emit(SignalName.DeleteRequested);
        Apply();
    }

    // A tap on the card, outside its action buttons, opens it on release. Drag-scrolling the card
    // row sends ScrollBegin, which cancels the tap as Godot cancels a Button press in a ScrollContainer.
    // While the tap would open the card, the card shows the press tint (#325).
    public override void _GuiInput(InputEvent inputEvent)
    {
        if (PointerInput.TryGetPressPosition(inputEvent, out var pressed))
        {
            SetOpenPending(_creation?.CanOpen == true && !_actions.GetGlobalRect().HasPoint(GetGlobalTransform() * pressed));
        }
        else if (PointerInput.TryGetDragPosition(inputEvent, out var dragged) && _openPending)
        {
            SetPressShown(IsInside(dragged));
        }
        else if (PointerInput.TryGetReleasePosition(inputEvent, out var released) && _openPending)
        {
            SetOpenPending(false);
            if (IsInside(released))
            {
                Emit(SignalName.OpenRequested);
            }
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationScrollBegin)
        {
            SetOpenPending(false);
        }
    }

    private void SetOpenPending(bool pending)
    {
        _openPending = pending;
        SetPressShown(pending);
    }

    private void SetPressShown(bool shown)
    {
        if (_pressShown != shown)
        {
            _pressShown = shown;
            _pressOverlay.QueueRedraw();
        }
    }

    // The tint covers exactly the tap area: the card above its action bar, whose Copy and
    // Delete are buttons with their own tint.
    private void DrawPress()
    {
        if (!_pressShown || _frame.GetThemeStylebox("panel") is not StyleBoxFlat panel)
        {
            return;
        }

        var bottom = _actions.IsVisibleInTree()
            ? (_pressOverlay.GetGlobalTransform().AffineInverse() * _actions.GlobalPosition).Y
            : _pressOverlay.Size.Y;
        var corners = _actions.IsVisibleInTree()
            ? UiCorners.Top(panel.CornerRadiusTopLeft)
            : UiCorners.Uniform(panel.CornerRadiusTopLeft);
        UiPressFeedback.Draw(_pressOverlay, corners, new Rect2(0, 0, _pressOverlay.Size.X, bottom), UiTokens.Color.Panel, danger: false);
    }

    private bool IsInside(Vector2 position) => new Rect2(Vector2.Zero, Size).HasPoint(position);

    private void Apply()
    {
        if (_creation is not { } creation)
        {
            return;
        }

        GetNode<CreatureThumbnail>("%Thumbnail").Creature = creation.Creature;
        var fallback = GetNode<UiLabel>("%ThumbnailFallback");
        fallback.ShowText(creation.ThumbnailText);
        fallback.Visible = creation.Creature.Nodes.Count == 0;
        GetNode<UiLabel>("%Name").Text = creation.Name;
        var summary = GetNode<UiLabel>("%Summary");
        summary.Text = creation.SummaryText;
        summary.Visible = creation.SummaryText.Length > 0;
        ApplyTraining(creation.Training);

        GetNode<UiButton>("%Copy").Disabled = !creation.CanDuplicate;
        GetNode<UiButton>("%Delete").Visible = creation.CanDelete;
    }

    private void ApplyTraining(CreationCardTraining? training)
    {
        var trained = training is not null;
        GetNode<Control>("%Padlock").Visible = trained;
        GetNode<Control>("%Stats").Visible = trained;
        GetNode<Control>("%Trained").Visible = trained;
        if (training is null)
        {
            return;
        }

        GetNode<UiLabel>("%Distance").ShowText(training.DistanceText);
        GetNode<UiLabel>("%TopSpeed").ShowText(training.TopSpeedText);
        GetNode<UiLabel>("%Elevation").ShowText(training.ElevationText);
        GetNode<UiIcon>("%Map").IconId = MapIcon(training.MapId);
        GetNode<UiLabel>("%Generations").ShowText(training.GenerationsText);
    }

    private static UiIconId MapIcon(string mapId) => mapId switch
    {
        MapIds.Flat => UiIconId.MapFlat,
        _ => UiIconId.Map,
    };

    private void Emit(StringName signal)
    {
        if (_creation is { } creation)
        {
            EmitSignal(signal, creation.Id.ToString("D"), creation.Name);
        }
    }
}
