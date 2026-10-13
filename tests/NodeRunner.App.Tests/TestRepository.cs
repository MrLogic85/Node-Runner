namespace NodeRunner.App.Tests;

/// <summary>The checkout the tests run in, for tests that read or rewrite committed files.</summary>
internal static class TestRepository
{
    /// <summary>The folder holding <c>NodeRunner.slnx</c>, found above the test output folder.</summary>
    public static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NodeRunner.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find NodeRunner.slnx.");
    }
}
