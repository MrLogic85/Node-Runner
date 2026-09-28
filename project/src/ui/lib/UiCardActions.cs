using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// The action bar along the bottom of a flush card (reference <c>c_card_actions</c>). Every
/// <see cref="UiButton"/> placed directly in it becomes one cell: an equal share of the width,
/// drawn as a stacked icon over its caption with no frame of its own, so the whole cell is the
/// touch target. A hairline runs above the row and between the cells. A Tertiary button is the
/// destructive action and takes the danger colour. The outer cells round their bottom corners
/// to the card's, since a card inside a page cannot clip its content (<see cref="UiClip"/>);
/// the card's <see cref="UiCard.ClipContent"/> still draws its border over the cells.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiCardActions : Container, IUiButtonDesigner
{
    private static readonly UiButtonStyle _cell = new(
        UiButtonKind.Flat,
        UiTokens.Color.Transparent,
        UiTokens.Color.Transparent,
        UiTokens.Color.Ink,
        UiTokens.Color.Ink);

    private static readonly UiButtonStyle _dangerCell = new(
        UiButtonKind.Tertiary,
        UiTokens.Color.Transparent,
        UiTokens.Color.Transparent,
        UiTokens.Color.Danger,
        UiTokens.Color.Danger);

    public UiButtonDesign DesignFor(UiButton button)
    {
        var cells = Cells();
        var radius = UiSize.Radius.Large;
        return new(
            button.Kind == UiButtonKind.Tertiary ? _dangerCell : _cell,
            UiButtonContentLayout.Stacked,
            new UiCorners(0, 0, cells.LastOrDefault() == button ? radius : 0, cells.FirstOrDefault() == button ? radius : 0));
    }

    public override void _Notification(int what)
    {
        if (what == NotificationSortChildren)
        {
            LayoutCells();
            QueueRedraw();
        }
        else if (what == NotificationThemeChanged)
        {
            UpdateMinimumSize();
            QueueSort();
        }
    }

    public override Vector2 _GetMinimumSize()
    {
        var cells = Cells();
        if (cells.Count == 0)
        {
            return Vector2.Zero;
        }

        var hair = Hair;
        var widest = cells.Max(cell => cell.GetCombinedMinimumSize().X);
        var tallest = cells.Max(cell => cell.GetCombinedMinimumSize().Y);
        return new Vector2((widest * cells.Count) + (hair * (cells.Count - 1)), hair + tallest);
    }

    public override void _Draw()
    {
        var hair = Hair;
        var color = UiThemeLookup.Color(this, UiTokens.Color.Line);
        DrawRect(new Rect2(0, 0, Size.X, hair), color);
        foreach (var cell in Cells().Skip(1))
        {
            DrawRect(new Rect2(cell.Position.X - hair, hair, hair, Size.Y - hair), color);
        }
    }

    // Cells fill their share regardless of their own size flags: a stacked button would
    // otherwise shrink to its fixed touch square in the middle of the cell.
    private void LayoutCells()
    {
        var cells = Cells();
        if (cells.Count == 0)
        {
            return;
        }

        var hair = Hair;
        var width = (Size.X - (hair * (cells.Count - 1))) / cells.Count;
        for (var index = 0; index < cells.Count; index++)
        {
            cells[index].Position = new Vector2(index * (width + hair), hair);
            cells[index].Size = new Vector2(width, Size.Y - hair);
            (cells[index] as UiButton)?.RefreshDesign();
        }
    }

    private List<Control> Cells() =>
        GetChildren().OfType<Control>().Where(child => child.Visible && !child.IsSetAsTopLevel()).ToList();

    private float Hair => UiThemeLookup.Size(UiTokens.Size.Stroke.Hair);
}
