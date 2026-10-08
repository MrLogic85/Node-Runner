namespace NodeRunner.App.ViewModels;

/// <summary>
/// The name an example copied into Creations gets (#840): its own name, or "Copy of …" when a
/// creation already has that name, compared as the player reads it (case ignored). A duplicate is
/// always a "Copy of …".
/// </summary>
public static class CreationNames
{
    public static bool IsTaken(string name, IEnumerable<string> taken) =>
        taken.Any(other => string.Equals(other, name, StringComparison.CurrentCultureIgnoreCase));

    public static UiText CopyOf(string name) => UiText.Format("Copy of {0}", name);

    /// <summary><paramref name="name"/>, or "Copy of …" in the player's language, cut to <see cref="NameLimits.Creation"/>, when it is taken.</summary>
    public static string Free(string name, IEnumerable<string> taken, Func<UiText, string> inPlayerLanguage)
    {
        ArgumentNullException.ThrowIfNull(inPlayerLanguage);
        return IsTaken(name, taken) ? NameLimits.Fit(inPlayerLanguage(CopyOf(name)), NameLimits.Creation) : name;
    }
}
