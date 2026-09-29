using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// One saved creation on the Creations screen. The layout is authored in
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
        GetNode<UiLabel>("%Name").Text = creation.DisplayName;
        GetNode<UiChip>("%ExampleChip").Visible = creation.IsExample;
        GetNode<UiLabel>("%Summary").Text = creation.SummaryText;

        var progress = GetNode<UiSlider>("%AchievementProgress");
        progress.Visible = creation.AchievementProgress > 0;
        progress.HighPosition = creation.AchievementProgress;
        progress.LabelText = creation.AchievementProgressText;

        var credit = GetNode<UiLabel>("%UnlockCredit");
        credit.Text = creation.UnlockCreditText;
        credit.Visible = !string.IsNullOrWhiteSpace(creation.UnlockCreditText);

        GetNode<UiButton>("%Copy").Disabled = !creation.CanDuplicate;
        var delete = GetNode<UiButton>("%Delete");
        delete.Disabled = !creation.CanDelete;
        delete.Visible = !creation.IsExample;
    }

    private void Emit(StringName signal)
    {
        if (_creation is { } creation)
        {
            EmitSignal(signal, creation.Id.ToString("D"), creation.Name);
        }
    }
}
