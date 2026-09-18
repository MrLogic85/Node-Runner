using System.Text.Json.Serialization;

namespace NodeRunner.Domain;

public readonly record struct Vector2D
{
    [JsonConstructor]
    public Vector2D(double x, double y)
    {
        if (!double.IsFinite(x))
        {
            throw new ArgumentOutOfRangeException(nameof(x), "X coordinate must be finite.");
        }

        if (!double.IsFinite(y))
        {
            throw new ArgumentOutOfRangeException(nameof(y), "Y coordinate must be finite.");
        }

        X = x;
        Y = y;
    }

    [JsonPropertyName("x")]
    public double X { get; }

    [JsonPropertyName("y")]
    public double Y { get; }
}
