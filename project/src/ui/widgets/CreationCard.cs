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
    private bool _openPending;

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
        GetNode<UiButton>("%Copy").Activated += () => Emit(SignalName.DuplicateRequested);
        GetNode<UiButton>("%Delete").Activated += () => Emit(SignalName.DeleteRequested);
        Apply();
    }

    // A tap on the card, outside its action buttons, opens it on release. Drag-scrolling the card
    // row sends ScrollBegin, which cancels the tap as Godot cancels a Button press in a ScrollContainer.
    public override void _GuiInput(InputEvent inputEvent)
    {
        if (PointerInput.TryGetPressPosition(inputEvent, out var pressed))
        {
            _openPending = _creation?.CanOpen == true && !_actions.GetGlobalRect().HasPoint(GetGlobalTransform() * pressed);
        }
        else if (PointerInput.TryGetReleasePosition(inputEvent, out var released) && _openPending)
        {
            _openPending = false;
            if (new Rect2(Vector2.Zero, Size).HasPoint(released))
            {
                Emit(SignalName.OpenRequested);
            }
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationScrollBegin)
        {
            _openPending = false;
        }
    }

    private void Apply()
    {
        if (_creation is not { } creation)
        {
            return;
        }

        GetNode<CreatureThumbnail>("%Thumbnail").Creature = creation.Creature;
        var fallback = GetNode<UiLabel>("%ThumbnailFallback");
        fallback.Text = creation.ThumbnailText;
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

        GetNode<UiLabel>("%Distance").Text = training.DistanceText;
        GetNode<UiLabel>("%TopSpeed").Text = training.TopSpeedText;
        GetNode<UiLabel>("%Elevation").Text = training.ElevationText;
        var map = GetNode<UiIcon>("%Map");
        map.IconId = MapIcon(training.MapId);
        map.Visible = map.IconId != UiIconId.None;
        GetNode<UiLabel>("%Generations").Text = training.GenerationsText;
    }

    private static UiIconId MapIcon(string? mapId) => mapId switch
    {
        null => UiIconId.None,
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
