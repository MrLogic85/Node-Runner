using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Widgets;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Import (#899): Paste reads a share code, the card previews its build or says why it was
/// refused, and Add to Creations saves it under the name in the Name field. The layout is authored in
/// <c>scenes/screens/ImportScreen.tscn</c>; this script binds the presentation and forwards Back
/// (the toolbar's, Android Back and Escape), Paste and Add.
/// </summary>
public partial class ImportScreen : Control
{
    private ImportPresentation _presentation = ImportPresentation.Waiting;

    [Signal]
    public delegate void BackRequestedEventHandler();

    [Signal]
    public delegate void PasteRequestedEventHandler();

    [Signal]
    public delegate void AddRequestedEventHandler(string name);

    public void Bind(ImportPresentation presentation)
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
        GetNode<UiButton>("%Paste").Activated += () => EmitSignal(SignalName.PasteRequested);
        GetNode<UiButton>("%Add").Activated += () => EmitSignal(SignalName.AddRequested, NameField.TextValue.Trim());
        NameField.ValidateValue = static value => !string.IsNullOrWhiteSpace(value);
        NameField.MaxLength = NameLimits.Creation;
        NameField.TextEdited += _ => ApplyName();
        UiNativeScroll.AllowGesturesToBubble(GetNode<Control>("%InfoColumn"));
        Apply();
    }

    private UiTextField NameField => GetNode<UiTextField>("%Name");

    private void RequestBack() => EmitSignal(SignalName.BackRequested);

    // A blank name cannot be saved, as in Build; a taken one can, but the line under the field says so.
    private void ApplyName()
    {
        GetNode<UiButton>("%Add").Disabled = !_presentation.CanAdd || string.IsNullOrWhiteSpace(NameField.TextValue);
        NameField.FactSource = UiTextTranslation.Source(_presentation.NameLine(NameField.TextValue));
    }

    private void Apply()
    {
        var preview = _presentation.State == ImportState.Preview;
        var thumbnail = GetNode<CreatureThumbnail>("%Thumbnail");
        thumbnail.Visible = preview;
        thumbnail.Creature = _presentation.Build?.Creature;
        GetNode<Control>("%NoteMargin").Visible = !preview;
        GetNode<Control>("%NoteIcon").Visible = _presentation.State == ImportState.Refused;
        ShowOptional(GetNode<UiLabel>("%NoteTitle"), UiTextTranslation.Source(_presentation.NoteTitle));
        ShowOptional(GetNode<UiLabel>("%NoteText"), UiTextTranslation.Source(_presentation.NoteText));
        GetNode<Control>("%InfoScroll").Visible = preview;
        NameField.TextValue = _presentation.Name ?? string.Empty;
        ApplyName();
        GetNode<UiButton>("%Paste").Kind = _presentation.PasteIsPrimary ? UiButtonKind.Primary : UiButtonKind.Secondary;
        GetNode<UiButton>("%Add").Kind = _presentation.PasteIsPrimary ? UiButtonKind.Secondary : UiButtonKind.Primary;
        ApplyParts();
    }

    private static void ShowOptional(UiLabel label, Func<string>? text)
    {
        label.Visible = text is not null;
        label.TextSource = text;
    }

    // One authored row per kind of part, named after it; a kind the build has none of stays hidden.
    private void ApplyParts()
    {
        var rows = GetNode<Control>("%PartRows");
        foreach (var row in rows.GetChildren().OfType<Control>())
        {
            row.Visible = false;
        }

        foreach (var part in _presentation.Parts)
        {
            var row = rows.GetNode<Control>(part.Kind.ToString());
            row.Visible = true;
            row.GetNode<UiLabel>("Count").ShowText(UiText.Number(part.Count));
        }
    }
}
