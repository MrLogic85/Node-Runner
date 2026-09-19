namespace NodeRunner.App.Repositories;

/// <summary>Provides an application-writable directory without exposing platform paths to the repository.</summary>
public interface IStorageLocation
{
    string DirectoryPath { get; }
}
