namespace NodeRunner.Ui.Tests;

/// <summary>
/// Reads settings from <c>project.godot</c> as text. Godot writes each setting as <c>key=value</c> under its
/// <c>[section]</c>, with the section's name left out of the key: <c>physics/2d/default_gravity</c> is
/// <c>2d/default_gravity=</c> under <c>[physics]</c> (#1252).
/// </summary>
internal static class ProjectGodot
{
    public static IEnumerable<string> Lines => File.ReadLines(Path.Combine(ThemeFile.ProjectRoot, "project.godot"));

    /// <summary>The raw value of <paramref name="key"/> in <paramref name="section"/>, or null when it is not set.</summary>
    public static string? Setting(IEnumerable<string> lines, string section, string key)
    {
        var header = $"[{section}]";
        var prefix = $"{key}=";
        var current = "";
        foreach (var line in lines)
        {
            if (line.StartsWith('['))
                current = line;
            else if (current == header && line.StartsWith(prefix, StringComparison.Ordinal))
                return line[prefix.Length..];
        }
        return null;
    }
}
