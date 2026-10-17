using System.Runtime.CompilerServices;

namespace NodeRunner.Domain.Tests;

/// <summary>One-line refusal rows, each named by its own source in a failure.</summary>
internal static class Refuses
{
    public static void OutOfRange(Action create, [CallerArgumentExpression(nameof(create))] string? row = null) =>
        Should.Throw<ArgumentOutOfRangeException>(create, row);

    public static ArgumentException Invalid(Action create, [CallerArgumentExpression(nameof(create))] string? row = null) =>
        Should.Throw<ArgumentException>(create, row);
}
