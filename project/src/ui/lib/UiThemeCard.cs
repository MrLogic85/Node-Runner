using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// One theme to pick: a <see cref="UiThemePreview"/> of it over its name, with an optional icon
/// before the name. The chosen card has the <c>sel</c> frame and a check by its name; the check's
/// place is kept on the others, so the name never moves. A tap picks it on release, as a Button
/// would, and dragging off or scrolling cancels the tap; while held, a card not yet chosen shows
/// the press tint.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiThemeCard : UiCard
{
    [Signal]
    public delegate void PickedEventHandler();

    private UiThemeChoice _choice = UiThemeChoice.Neon;
    private string _title = "Neon";
    private UiIconId _iconId = UiIconId.None;
    private bool _chosen;
    private bool _pickPending;
    private UiThemePreview? _preview;
    private UiIcon? _icon;
    private Label? _name;
    private UiIcon? _check;

    private readonly UiUnsavedState _unsaved = new([
        (".", UiCard.PropertyName.Kind),
        ("%Preview", UiThemePreview.PropertyName.Choice),
        ("%Icon", UiIcon.PropertyName.IconId),
        ("%Icon", CanvasItem.PropertyName.Visible),
        ("%Name", Label.PropertyName.Text),
        ("%Check", UiIcon.PropertyName.IconId),
    ]);

    [Export]
    public UiThemeChoice Choice
    {
        get => _choice;
        set
        {
            _choice = value;
            Apply();
        }
    }

    [Export]
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            Apply();
        }
    }

    [Export]
    public UiIconId IconId
    {
        get => _iconId;
        set
        {
            _iconId = value;
            Apply();
        }
    }

    [Export]
    public bool Chosen
    {
        get => _chosen;
        set
        {
            _chosen = value;
            Apply();
        }
    }

    public override void _Ready()
    {
        _preview = GetNode<UiThemePreview>("%Preview");
        _icon = GetNode<UiIcon>("%Icon");
        _name = GetNode<Label>("%Name");
        _check = GetNode<UiIcon>("%Check");
        base._Ready();
        Apply();
    }

    public override void _Notification(int what)
    {
        // Before the base: clearing Kind restyles the card, so the base must clear the panel style after it.
        var saving = _unsaved.Handle(this, what, Apply);
        base._Notification(what);
        if (!saving && what == NotificationScrollBegin)
        {
            SetPickPending(false);
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (PointerInput.TryGetPressPosition(@event, out _))
        {
            SetPickPending(true);
        }
        else if (PointerInput.TryGetDragPosition(@event, out var dragged) && !IsInside(dragged))
        {
            SetPickPending(false);
        }
        else if (PointerInput.TryGetReleasePosition(@event, out var released) && _pickPending)
        {
            SetPickPending(false);
            if (IsInside(released))
            {
                EmitSignal(SignalName.Picked);
                AcceptEvent();
            }
        }
    }

    private void SetPickPending(bool pending)
    {
        _pickPending = pending;
        ShowsPress = pending && !Chosen;
    }

    private bool IsInside(Vector2 position) => new Rect2(Vector2.Zero, Size).HasPoint(position);

    private void Apply()
    {
        Kind = Chosen ? CardVariant.Selected : CardVariant.Frame;
        if (_preview is null || _icon is null || _name is null || _check is null)
        {
            return;
        }

        _preview.Choice = Choice;
        // The card cannot clip inside a clipping page (UiClip), so the picture rounds its own corners.
        if (GetThemeStylebox("panel") is StyleBoxFlat panel)
        {
            _preview.TopRadius = Math.Max(0, panel.CornerRadiusTopLeft - panel.ContentMarginTop);
        }

        _icon.IconId = IconId;
        _icon.Visible = IconId != UiIconId.None;
        UiTranslation.ShareContext(this, _name);
        _name.Text = Title;
        _check.IconId = Chosen ? UiIconId.Check : UiIconId.None;
    }
}
