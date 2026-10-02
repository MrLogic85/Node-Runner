using System.Text.Json;
using System.Text.Json.Serialization;

namespace NodeRunner.App.Repositories;

/// <summary>
/// The one JSON setup for every save file (docs/SAVE_FORMAT.md). Every field is written, optional
/// ones as <c>null</c>. Loading is as strict as the schema in docs/save-schema/: an unknown field, a
/// missing required field or a <c>null</c> where none is allowed fails with a
/// <see cref="JsonException"/> that names it.
/// </summary>
public static class SaveJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json, string path)
        where T : class =>
        JsonSerializer.Deserialize<T>(json, Options)
            ?? throw new InvalidDataException($"Save file '{path}' is empty or invalid.");

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true,
            RespectNullableAnnotations = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
