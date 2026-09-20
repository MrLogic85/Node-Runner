using NodeRunner.App.Repositories;

namespace NodeRunner.App.Tests.Repositories;

public sealed class FilePersistenceExceptionsTests
{
    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(System.Text.Json.JsonException))]
    [InlineData(typeof(InvalidDataException))]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(ArgumentOutOfRangeException))]
    [InlineData(typeof(KeyNotFoundException))]
    public void IsRecoverable_RecognizesEveryExceptionTypeThrownByFilePersistenceCode(Type exceptionType)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        FilePersistenceExceptions.IsRecoverable(exception).ShouldBeTrue();
    }

    [Fact]
    public void IsRecoverable_DoesNotSwallowUnrelatedProgrammingErrors()
    {
        // NullReferenceException/IndexOutOfRangeException etc. indicate a bug
        // in our own code, not a corrupt/missing file -- they must keep
        // propagating instead of being treated as recoverable (#114).
        FilePersistenceExceptions.IsRecoverable(new NullReferenceException()).ShouldBeFalse();
    }

    [Fact]
    public void IsRecoverable_DoesNotSwallowSaveManagersNotReadyLifecycleBug()
    {
        // SaveManager throws InvalidOperationException("SaveManager is not
        // ready.") from its composition-root guards if accessed before
        // _Ready() has run -- a genuine lifecycle bug, not a corrupt/missing
        // file. It must stay distinguishable from the "Creation not found"
        // case (KeyNotFoundException) so it keeps failing loud (#114).
        FilePersistenceExceptions.IsRecoverable(new InvalidOperationException("SaveManager is not ready.")).ShouldBeFalse();
    }
}
