using System.Text.Json;

namespace NodeRunner.App.Repositories;

/// <summary>
/// Single source of truth for "is this exception a recoverable
/// unreadable/corrupt/missing on-disk Creation or Progression, rather than a
/// programming error I should let crash?" Used by every file read/write/
/// lookup guard (repositories and Main.cs) so the set can't drift between
/// call sites (#114).
/// </summary>
public static class FilePersistenceExceptions
{
    public static bool IsRecoverable(Exception ex) =>
        ex is IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidDataException
            or ArgumentException
            or KeyNotFoundException;
}
