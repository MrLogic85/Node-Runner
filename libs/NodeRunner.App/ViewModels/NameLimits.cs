using System.Text;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// How long a name the player can give (#868): a Creation up to <see cref="Creation"/> characters,
/// a part up to <see cref="Part"/>, so a part's name fits inside its brain labels. Name fields stop
/// at the limit while the player types; a name the game makes, such as "Copy of {0}", is cut with
/// <see cref="Fit"/>. A saved name that is already longer is kept until the player edits it.
/// Characters are counted as Unicode code points, like Godot's <c>LineEdit.MaxLength</c>.
/// </summary>
public static class NameLimits
{
    public const int Creation = 40;

    public const int Part = 10;

    /// <summary><paramref name="name"/> cut to at most <paramref name="limit"/> characters, without trailing spaces left by the cut.</summary>
    public static string Fit(string name, int limit)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        var runes = name.EnumerateRunes().ToArray();
        if (runes.Length <= limit)
        {
            return name;
        }

        var cut = new StringBuilder();
        foreach (var rune in runes.Take(limit))
        {
            cut.Append(rune.ToString());
        }

        return cut.ToString().TrimEnd();
    }
}
