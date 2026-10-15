namespace NodeRunner.Arch.Tests;

/// <summary>The checkout the tests run from.</summary>
internal static class RepositoryRoot
{
    /// <summary>The nearest parent directory of the test binaries that holds NodeRunner.slnx.</summary>
    internal static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NodeRunner.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find NodeRunner.slnx.");
    }
}
