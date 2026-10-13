namespace NodeRunner.App.Tests.Repositories;

/// <summary>
/// Compares a text with its committed pin in Repositories/Pins, naming the first line that differs,
/// or rewrites the pin when the update variable is set (docs/SAVE_FORMAT.md → "Pinned saves").
/// </summary>
internal static class Pin
{
    private const string _updateVariable = "NODE_RUNNER_UPDATE_PINS";

    private static readonly string _folder = Path.Combine("tests", "NodeRunner.App.Tests", "Repositories", "Pins");

    public static void Match(string actual, string name)
    {
        var path = Path.Combine(TestRepository.Root(), _folder, name);
        if (Environment.GetEnvironmentVariable(_updateVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual);
            return;
        }

        File.Exists(path).ShouldBeTrue($"No pin {name}: run the test with {_updateVariable}=1 to write it.");
        var expected = File.ReadAllText(path).ReplaceLineEndings("\n");
        if (expected == actual)
        {
            return;
        }

        var expectedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');
        var line = Enumerable.Range(0, Math.Max(expectedLines.Length, actualLines.Length))
            .First(index => LineAt(expectedLines, index) != LineAt(actualLines, index));
        Assert.Fail(
            $"{name} changed at line {line + 1}.\n" +
            $"  pinned: {LineAt(expectedLines, line) ?? "(end of file)"}\n" +
            $"  now:    {LineAt(actualLines, line) ?? "(end of file)"}\n" +
            $"If the change is deliberate, rewrite the pins with {_updateVariable}=1 and follow docs/SAVE_FORMAT.md → \"Pinned saves\".");
    }

    private static string? LineAt(string[] lines, int index) => index < lines.Length ? lines[index] : null;
}
