using System.Text.Json;

namespace NodeRunner.App.Repositories;

/// <summary>
/// Whether an exception is a recoverable unreadable, corrupt or missing Creation or Progression
/// file rather than a programming error that should crash. Every file guard shares this one set
/// so it cannot drift.
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
