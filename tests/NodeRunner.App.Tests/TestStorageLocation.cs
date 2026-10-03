using NodeRunner.App.Repositories;

namespace NodeRunner.App.Tests;

/// <summary>A save folder at <paramref name="directoryPath"/>, for tests that write real files.</summary>
internal sealed class TestStorageLocation(string directoryPath) : IStorageLocation
{
    public string DirectoryPath { get; } = directoryPath;
}
