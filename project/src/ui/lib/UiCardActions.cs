using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// The action bar along the bottom of a flush card (reference <c>c_card_actions</c>). Every
/// <see cref="UiButton"/> placed directly in it becomes one cell: an equal share of the width,
/// drawn as a stacked icon over its caption (or the caption alone) with no frame of its own, so
/// the whole cell is the touch target. A hairline runs above the row and between the cells. The
/// button's kind only picks the text colour: Primary is accent, Tertiary (destructive) is danger,
/// Secondary and Flat are ink. The outer cells round their bottom corners
/// to the card's, since a card inside a page cannot clip its content (<see cref="UiClip"/>);
/// the card's <see cref="UiCard.ClipContent"/> still draws its border over the cells.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiCardActions : Container, IUiButtonDesigner
{
    private static readonly UiButtonStyle _cell = Cell(UiButtonKind.Flat, UiTokens.Color.Ink);
    private static readonly UiButtonStyle _accentCell = Cell(UiButtonKind.Primary, UiTokens.Color.Accent);
    private static readonly UiButtonStyle _dangerCell = Cell(UiButtonKind.Tertiary, UiTokens.Color.Danger);

    public UiButtonDesign DesignFor(UiButton button)
    {
        var cells = Cells();
        var radius = UiSize.Radius.Large;
        return new(
            button.Kind switch
            {
                UiButtonKind.Primary => _accentCell,
                UiButtonKind.Tertiary => _dangerCell,
                _ => _cell,
            },
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
        using var pen = UiPixelPen.Begin(this);
        pen.Line(new Vector2(0, hair * 0.5f), new Vector2(Size.X, hair * 0.5f), color, hair);
        foreach (var cell in Cells().Skip(1))
        {
            var x = cell.Position.X - (hair * 0.5f);
            pen.Line(new Vector2(x, hair), new Vector2(x, Size.Y), color, hair);
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

    private static UiButtonStyle Cell(UiButtonKind kind, UiTokens.Color content) =>
        new(kind, UiTokens.Color.Transparent, UiTokens.Color.Transparent, content, content);

    private float Hair => UiThemeLookup.Size(UiTokens.Size.Stroke.Hair);
}
