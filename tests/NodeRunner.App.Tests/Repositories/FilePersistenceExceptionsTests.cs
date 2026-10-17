using NodeRunner.App.Repositories;

namespace NodeRunner.App.Tests.Repositories;

public sealed class FilePersistenceExceptionsTests
{
    // Every type file persistence throws is recoverable. A bug in our own code, or SaveManager's
    // "not ready" lifecycle guard, must keep failing loud, distinct from "Creation not found" (#114).
    [Theory]
    [InlineData(typeof(IOException), true)]
    [InlineData(typeof(UnauthorizedAccessException), true)]
    [InlineData(typeof(System.Text.Json.JsonException), true)]
    [InlineData(typeof(InvalidDataException), true)]
    [InlineData(typeof(ArgumentException), true)]
    [InlineData(typeof(KeyNotFoundException), true)]
    [InlineData(typeof(InvalidOperationException), false)]
    public void IsRecoverable_RecognizesWhatFilePersistenceThrows(Type exceptionType, bool recoverable)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        FilePersistenceExceptions.IsRecoverable(exception).ShouldBe(recoverable);
    }
}
