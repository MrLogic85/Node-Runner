using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>The grid sizes as the player picks them: by place in <see cref="BuildGridDef.SizesCm"/>, labelled in metres.</summary>
public static class BuildGridSizes
{
    /// <summary>Each size in metres, in the order of <see cref="BuildGridDef.SizesCm"/>.</summary>
    public static IReadOnlyList<UiText> Labels { get; } =
        [.. BuildGridDef.SizesCm.Select(size => UiText.Format("{0} m", new FixedNumber(size / 100.0, DecimalsOf(size))))];

    /// <summary>The place of <paramref name="sizeCm"/> in <see cref="BuildGridDef.SizesCm"/>.</summary>
    public static int IndexOf(int sizeCm) => BuildGridDef.SizesCm.ToList().IndexOf(sizeCm);

    /// <summary>The size in cm at <paramref name="index"/> in <see cref="BuildGridDef.SizesCm"/>.</summary>
    public static int SizeCmAt(int index) => BuildGridDef.SizesCm[index];

    // As few as the size needs: 0.25, 0.5 and 1.
    private static int DecimalsOf(int sizeCm) => sizeCm % 100 == 0 ? 0 : sizeCm % 10 == 0 ? 1 : 2;
}
