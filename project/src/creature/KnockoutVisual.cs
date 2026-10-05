using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// The <see cref="KnockoutPart"/> around one beam or node in Training (#818): only the followed
/// creature needs to stand out from the shadows behind it.
/// </summary>
public partial class KnockoutVisual : KnockoutPart, IShadowVisual
{
    public static ShadowDrawing AsShadow => ShadowDrawing.Hidden;

    public bool IsShadow
    {
        get => !Visible;
        set => Visible = !value;
    }
}
