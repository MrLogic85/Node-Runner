using NodeRunner.App.Repositories;

namespace NodeRunner.Managers;

/// <summary>A folder under Godot's <c>user://</c>, already globalized to a platform path.</summary>
internal sealed class GodotStorageLocation(string directoryPath) : IStorageLocation
{
    public string DirectoryPath { get; } = directoryPath;
}
