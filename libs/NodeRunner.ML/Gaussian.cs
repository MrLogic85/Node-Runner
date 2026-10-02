namespace NodeRunner.ML;

/// <summary>Standard normal samples, for mutation and generation-0 noise.</summary>
internal static class Gaussian
{
    // Box-Muller transform: turns two uniform samples into one standard normal sample. u1 is
    // drawn from (0, 1] (never exactly 0) so Log(u1) is always defined.
    public static double Next(Random random)
    {
        var u1 = 1.0 - random.NextDouble();
        var u2 = random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
