namespace NodeRunner.Ui.Lib;

/// <summary>
/// How a <see cref="UiButton"/> is drawn: its colours, how icon and caption are laid out, and
/// the corners of its frame. A button uses its own design unless its direct parent is an
/// <see cref="IUiButtonDesigner"/>; its behaviour (disabled, badge) never changes.
/// </summary>
public readonly record struct UiButtonDesign(
    UiButtonStyle Style,
    UiButtonContentLayout ContentLayout,
    UiCorners Corners);

/// <summary>
/// A container that gives the <see cref="UiButton"/>s placed directly in it a look of its own,
/// such as the cells of <see cref="UiCardActions"/>. The container also owns their size.
/// </summary>
public interface IUiButtonDesigner
{
    UiButtonDesign DesignFor(UiButton button);
}
