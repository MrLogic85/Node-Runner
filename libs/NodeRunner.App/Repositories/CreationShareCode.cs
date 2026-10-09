using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NodeRunner.App.Builders;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Repositories;

/// <summary>Why a pasted share code was refused (#899).</summary>
public enum ShareCodeRefusal
{
    /// <summary>Nothing but white space was pasted.</summary>
    Empty,

    /// <summary>The text is not a share code at all.</summary>
    NotACreation,

    /// <summary>A share code that is cut short, changed, or fails to load.</summary>
    Damaged,

    /// <summary>A share code from an app with a newer save format.</summary>
    NewerVersion,

    /// <summary>A share code whose creature has no joints.</summary>
    NothingToBuild,
}

/// <summary>
/// A read share code: the build it holds, or why it was refused. <paramref name="UpdatedFrom"/> is
/// the format version of a code older than this app's, which its build was migrated from (#1094).
/// </summary>
public sealed record ShareCodeRead(CreationDef? Build, ShareCodeRefusal? Refusal, int? UpdatedFrom = null)
{
    public static ShareCodeRead Refused(ShareCodeRefusal refusal) => new(null, refusal);
}

/// <summary>
/// A Creation's build as one text to copy and paste (#899): <c>NR6.</c> and its <c>creation.json</c>,
/// build only, rounded, renumbered and without what its version's share dictionary already says
/// (#1094), compressed against that dictionary, in URL-safe base64. It goes through the same
/// migrations as a saved file. docs/SAVE_FORMAT.md → "Share code" owns the format.
/// </summary>
public static class CreationShareCode
{
    /// <summary>The longest code read, white space left out; a longer text is refused as damaged.</summary>
    public const int MaxLength = 32 * 1024;

    /// <summary>The most JSON a code may unpack to, so a small code can't unpack to a huge one.</summary>
    public const int MaxJsonBytes = 1024 * 1024;

    // Every code starts NR, its format version and a dot, as in NR6.
    private const string _start = "NR";
    private const char _versionEnd = '.';

    // Every code's id, which Import replaces; a random one would cost about 35 characters.
    private static readonly Guid _placeholderId = new("00000000-0000-0000-0000-000000000001");

    // The decimals a setting keeps in a code; a joint keeps whole world units (CreatureBuilder.OnWholeUnits).
    private const int _settingDecimals = 3;

    /// <summary>Every field of a part that holds a part id, renumbered with it.</summary>
    internal static IReadOnlySet<string> PartIdFields { get; } = new HashSet<string> { "id", "nodeA", "nodeB", "beamId", "nodeId", "fixedLinkId", "targetLinkId" };

    private const string _idField = "id";

    private const string _nextPartIdField = "nextPartId";

    private static readonly JsonSerializerOptions _compact = new(SaveJson.Options) { WriteIndented = false };

    /// <summary>The code for <paramref name="creation"/>'s build: its name, its parts and their names and settings.</summary>
    public static string Create(CreationDef creation)
    {
        ArgumentNullException.ThrowIfNull(creation);
        var version = FileCreationRepository.Format.CurrentVersion;
        var dictionary = DictionaryOrThrow(version);
        return Pack(version, dictionary, Encoding.UTF8.GetBytes(Compact(Template(dictionary), Rounded(creation))));
    }

    /// <summary>The build in <paramref name="text"/>, or why it was refused. Never throws for bad input.</summary>
    public static ShareCodeRead Read(string? text)
    {
        var code = string.Concat((text ?? string.Empty).Where(c => !char.IsWhiteSpace(c)));
        if (code.Length == 0)
        {
            return ShareCodeRead.Refused(ShareCodeRefusal.Empty);
        }

        if (!TrySplit(code, out var version, out var body))
        {
            return ShareCodeRead.Refused(ShareCodeRefusal.NotACreation);
        }

        if (version > FileCreationRepository.Format.CurrentVersion)
        {
            return ShareCodeRead.Refused(ShareCodeRefusal.NewerVersion);
        }

        if (code.Length > MaxLength || Dictionary(version) is not { } dictionary || Unpack(dictionary, body) is not { } compact)
        {
            return ShareCodeRead.Refused(ShareCodeRefusal.Damaged);
        }

        CreationDef creation;
        try
        {
            var json = Expand(dictionary, compact);
            if (FileCreationRepository.Format.IsNewer(json))
            {
                return ShareCodeRead.Refused(ShareCodeRefusal.NewerVersion);
            }

            creation = FileCreationRepository.Format.Deserialize(json, "share code").Value;
        }
        // The migrations were written for the app's own files, and this is anyone's text: whatever
        // they or the strict load throw on it, the code is damaged.
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return ShareCodeRead.Refused(ShareCodeRefusal.Damaged);
        }

        if (creation.Creature.Nodes.Count == 0)
        {
            return ShareCodeRead.Refused(ShareCodeRefusal.NothingToBuild);
        }

        // Only a code changed by hand gives a name that fits to nothing or a build Build could not make.
        var name = NameLimits.Fit(creation.Name, NameLimits.Creation);
        return name.Length == 0 || !new CreatureBuilder(creation.Creature).IsWithinBuildLimits()
            ? ShareCodeRead.Refused(ShareCodeRefusal.Damaged)
            : new ShareCodeRead(
                new CreationDef(creation.Id, name, creation.Creature),
                null,
                version < FileCreationRepository.Format.CurrentVersion ? version : null);
    }

    /// <summary>
    /// A new format version's share dictionary (docs/SAVE_FORMAT.md → "Share code"). Its first line is
    /// <paramref name="template"/>'s <c>creation.json</c>, whose values a code leaves out; its second is
    /// <paramref name="sample"/> as a code holds it, so a code finds its shape there too.
    /// </summary>
    internal static byte[] MakeDictionary(CreationDef template, CreationDef sample)
    {
        var templateFile = FileOf(template);
        return Encoding.UTF8.GetBytes($"{templateFile.ToJsonString(_compact)}\n{Compact(templateFile, Rounded(sample))}\n");
    }

    /// <summary>
    /// <paramref name="creation"/> as a code holds it: its joints on whole world units and every other
    /// setting to <see cref="_settingDecimals"/> decimals, as long as its slider still allows it.
    /// </summary>
    internal static CreationDef Rounded(CreationDef creation)
    {
        var builder = new CreatureBuilder(creation.Creature);
        foreach (var node in builder.Nodes.ToArray())
        {
            builder.MoveNode(node.Id, CreatureBuilder.OnWholeUnits(node.Position));
        }

        foreach (var partId in builder.PartIdsWithSettings.ToArray())
        {
            foreach (var parameter in builder.ParametersOf(partId))
            {
                var value = builder.ParameterValue(partId, parameter);
                var rounded = Math.Round(value, _settingDecimals) + 0.0;
                if (rounded != value && CreatureBuilder.IsOnItsSlider(parameter, rounded))
                {
                    builder.SetParameter(partId, parameter, rounded);
                }
            }
        }

        return new CreationDef(creation.Id, creation.Name, builder.Build());
    }

    // creation's file with its part ids renumbered (Renumber), then left out wherever it says what
    // template says (Strip).
    private static string Compact(JsonObject template, CreationDef creation)
    {
        var file = FileOf(creation);
        Renumber(file["creature"]!.AsObject(), template["creature"]!.AsObject());
        Strip(file, template);
        return file.ToJsonString(_compact);
    }

    // creation.json with the placeholder id and no training.
    private static JsonObject FileOf(CreationDef creation) =>
        JsonNode.Parse(FileCreationRepository.Format.Serialize(new CreationDef(_placeholderId, creation.Name, creation.Creature)))!.AsObject();

    // A dictionary's first line: the creation.json whose values a code leaves out.
    private static JsonObject Template(byte[] dictionary)
    {
        var lineEnd = Array.IndexOf(dictionary, (byte)'\n');
        return JsonNode.Parse(lineEnd < 0 ? dictionary : dictionary.AsSpan(0, lineEnd))!.AsObject();
    }

    // The creature's lists of parts, in the template's order.
    private static IEnumerable<JsonArray> PartLists(JsonObject creature, JsonObject templateCreature) =>
        templateCreature.Where(field => field.Value is JsonArray).Select(field => creature[field.Key]).OfType<JsonArray>();

    // Numbers the parts 1, 2, 3 … in the order of their ids, which keeps the brain's ports in their
    // order (BrainPorts) and is the order they were added in. A part
    // then leaves out its id when it is one more than the one before it in its list, and the
    // creature its next part id, which is one more than its last; Number puts them back.
    private static void Renumber(JsonObject creature, JsonObject templateCreature)
    {
        var parts = PartLists(creature, templateCreature).SelectMany(list => list).OfType<JsonObject>().ToArray();
        var newIds = parts.Select(part => part[_idField]!.GetValue<int>()).Order()
            .Select((id, index) => (id, index)).ToDictionary(entry => entry.id, entry => entry.index + 1);
        foreach (var part in parts)
        {
            foreach (var (field, value) in part.ToArray())
            {
                if (PartIdFields.Contains(field) && value is JsonValue id)
                {
                    part[field] = newIds[id.GetValue<int>()];
                }
            }
        }

        foreach (var list in PartLists(creature, templateCreature))
        {
            var previous = 0;
            foreach (var part in list.OfType<JsonObject>())
            {
                var id = part[_idField]!.GetValue<int>();
                if (id == previous + 1)
                {
                    part.Remove(_idField);
                }

                previous = id;
            }
        }

        creature.Remove(_nextPartIdField);
    }

    // Undoes Renumber's leaving out: a part without an id is one more than the one before it in its
    // list, and a creature without a next part id is one more than its largest.
    private static void Number(JsonObject creature, JsonObject templateCreature)
    {
        var largest = 0;
        foreach (var list in PartLists(creature, templateCreature))
        {
            var previous = 0;
            foreach (var part in list.OfType<JsonObject>())
            {
                if (!part.ContainsKey(_idField))
                {
                    part[_idField] = previous + 1;
                }

                previous = part[_idField]!.GetValue<int>();
                largest = Math.Max(largest, previous);
            }
        }

        if (!creature.ContainsKey(_nextPartIdField))
        {
            creature[_nextPartIdField] = largest + 1;
        }
    }

    /// <summary>
    /// Leaves out of <paramref name="file"/> each field equal to <paramref name="template"/>'s, or to
    /// it rounded as <see cref="Rounded"/> rounds: an object field by field, an empty list whole, and
    /// the nth part in a list against the template's nth in that list, or its first past its last.
    /// A part's id is Renumber's to leave out. <see cref="Fill"/> puts it all back.
    /// </summary>
    private static void Strip(JsonObject file, JsonObject template, bool isPart = false)
    {
        foreach (var (field, value) in file.ToArray())
        {
            if ((isPart && field == _idField) || !template.TryGetPropertyValue(field, out var model))
            {
                continue;
            }

            switch (value, model)
            {
                case (JsonArray list, JsonArray models):
                    if (list.Count == 0)
                    {
                        file.Remove(field);
                    }
                    else
                    {
                        foreach (var (part, partModel) in PartsAndModels(list, models))
                        {
                            Strip(part, partModel, isPart: true);
                        }
                    }

                    break;
                case (JsonObject inner, JsonObject innerModel):
                    Strip(inner, innerModel);
                    if (inner.Count == 0)
                    {
                        file.Remove(field);
                    }

                    break;
                default:
                    if (Text(value) == Text(model) || IsRoundedFrom(value, model))
                    {
                        file.Remove(field);
                    }

                    break;
            }
        }
    }

    // A code's JSON with what Renumber and Strip left out put back, so it is a whole file again.
    private static string Expand(byte[] dictionary, string compact)
    {
        var file = JsonNode.Parse(compact) as JsonObject
            ?? throw new InvalidDataException("A share code's JSON is not an object.");
        var template = Template(dictionary);
        if (file["creature"] is JsonObject creature && template["creature"] is JsonObject templateCreature)
        {
            if (FilledBytes(creature, templateCreature) > MaxJsonBytes)
            {
                throw new InvalidDataException("A share code fills out to more than the cap.");
            }

            Number(creature, templateCreature);
        }

        Fill(file, template);
        return file.ToJsonString(_compact);
    }

    // At most what Fill adds to creature's parts: each as long as the longest template part in its
    // list. A part costs a code 3 bytes as {}, so without this a short code could fill out to a huge one.
    private static long FilledBytes(JsonObject creature, JsonObject templateCreature) =>
        templateCreature.Where(field => field.Value is JsonArray { Count: > 0 })
            .Sum(field => creature[field.Key] is JsonArray list
                ? (long)list.Count * field.Value!.AsArray().Max(model => Text(model)?.Length ?? 0)
                : 0);

    // Undoes Strip: a missing list is empty, and any other missing field is the template's.
    private static void Fill(JsonObject file, JsonObject template)
    {
        foreach (var (field, model) in template)
        {
            if (!file.TryGetPropertyValue(field, out var value))
            {
                file[field] = model switch
                {
                    JsonArray => new JsonArray(),
                    JsonObject innerModel => Filled(new JsonObject(), innerModel),
                    _ => model?.DeepClone(),
                };
            }
            else if (value is JsonObject inner && model is JsonObject innerModel)
            {
                Fill(inner, innerModel);
            }
            else if (value is JsonArray list && model is JsonArray models)
            {
                foreach (var (part, partModel) in PartsAndModels(list, models))
                {
                    Fill(part, partModel);
                }
            }
        }
    }

    private static JsonObject Filled(JsonObject file, JsonObject template)
    {
        Fill(file, template);
        return file;
    }

    // Each part in list with the template part it is compared with: the one at its index, or the first.
    private static IEnumerable<(JsonObject Part, JsonObject Model)> PartsAndModels(JsonArray list, JsonArray models)
    {
        for (var index = 0; index < list.Count && models.Count > 0; index++)
        {
            if (list[index] is JsonObject part && models[index < models.Count ? index : 0] is JsonObject model)
            {
                yield return (part, model);
            }
        }
    }

    private static string? Text(JsonNode? node) => node?.ToJsonString(_compact);

    // Whether value is model rounded as Rounded rounds, like a Servo's default range π as 3.142, so it reads back as π.
    private static bool IsRoundedFrom(JsonNode? value, JsonNode? model) =>
        value is JsonValue rounded && rounded.TryGetValue<double>(out var number)
        && model is JsonValue exact && exact.TryGetValue<double>(out var modelNumber)
        && Math.Round(modelNumber, _settingDecimals) == number;

    /// <summary>
    /// The share dictionary for <paramref name="version"/>, or null when it has none (see
    /// <see cref="MakeDictionary"/>). One is added with each new format version and never changed, or
    /// older codes would no longer read.
    /// </summary>
    internal static byte[]? Dictionary(int version)
    {
        using var resource = typeof(CreationShareCode).Assembly.GetManifestResourceStream(DictionaryName(version));
        if (resource is null)
        {
            return null;
        }

        using var bytes = new MemoryStream();
        resource.CopyTo(bytes);
        return bytes.ToArray();
    }

    /// <summary>The file name of <paramref name="version"/>'s share dictionary in Repositories/ShareDictionaries/.</summary>
    internal static string DictionaryName(int version) => $"creation-v{version}.json";

    /// <summary>The code for <paramref name="json"/> in <paramref name="version"/>, whatever the bytes hold.</summary>
    internal static string Pack(int version, byte[] json) => Pack(version, DictionaryOrThrow(version), json);

    private static byte[] DictionaryOrThrow(int version) => Dictionary(version)
        ?? throw new InvalidOperationException($"Format version {version} has no share dictionary ({DictionaryName(version)}).");

    private static string Pack(int version, byte[] dictionary, byte[] json)
    {
        using var packed = new MemoryStream();
        long bodyStart;
        using (var zlib = new ZLibStream(packed, CompressionLevel.SmallestSize))
        {
            zlib.Write(dictionary);
            // A sync flush: the dictionary's bytes end on a whole byte, so the code can leave them out.
            zlib.Flush();
            bodyStart = packed.Length;
            zlib.Write(json);
        }

        var body = Convert.ToBase64String(packed.ToArray().AsSpan((int)bodyStart)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{_start}{version}{_versionEnd}{body}";
    }

    // Splits NR6.body into its version and body.
    private static bool TrySplit(string code, out int version, out string body)
    {
        version = 0;
        body = string.Empty;
        var versionEnd = code.IndexOf(_versionEnd, StringComparison.Ordinal);
        if (!code.StartsWith(_start, StringComparison.Ordinal) || versionEnd < 0)
        {
            return false;
        }

        var digits = code.AsSpan(_start.Length, versionEnd - _start.Length);
        if (digits.IsEmpty || digits.IndexOfAnyExceptInRange('0', '9') >= 0 || !int.TryParse(digits, out version))
        {
            return false;
        }

        body = code[(versionEnd + 1)..];
        return true;
    }

    // The code's JSON, or null when the body is not URL-safe base64, does not unpack against the
    // dictionary, fails its checksum, or unpacks to more than MaxJsonBytes. A body cut short at its
    // end skips the checksum; a JSON cut with it fails to load.
    private static string? Unpack(byte[] dictionary, string body)
    {
        if (!body.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
        {
            return null;
        }

        var base64 = body.Replace('-', '+').Replace('_', '/') + new string('=', (4 - (body.Length % 4)) % 4);
        var bytes = new byte[base64.Length];
        if (!Convert.TryFromBase64String(base64, bytes, out var length))
        {
            return null;
        }

        try
        {
            using var packed = new MemoryStream();
            packed.Write(DictionaryStart(dictionary));
            packed.Write(bytes, 0, length);
            packed.Position = 0;
            using var zlib = new ZLibStream(packed, CompressionMode.Decompress);
            using var unpacked = new MemoryStream();
            var buffer = new byte[16 * 1024];
            int read;
            while ((read = zlib.Read(buffer)) > 0)
            {
                if (unpacked.Length + read > dictionary.Length + MaxJsonBytes)
                {
                    return null;
                }

                unpacked.Write(buffer, 0, read);
            }

            var all = unpacked.GetBuffer().AsSpan(0, (int)unpacked.Length);
            return all.StartsWith(dictionary)
                ? new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(all[dictionary.Length..])
                : null;
        }
        catch (Exception ex) when (ex is InvalidDataException or DecoderFallbackException)
        {
            return null;
        }
    }

    // The packed dictionary up to the flush that ends it, so a code's body unpacks after it. Deflate
    // refers back to unpacked bytes only, so this needn't be the very bytes the code was made with.
    private static byte[] DictionaryStart(byte[] dictionary)
    {
        using var packed = new MemoryStream();
        using var zlib = new ZLibStream(packed, CompressionLevel.Fastest);
        zlib.Write(dictionary);
        zlib.Flush();
        return packed.ToArray();
    }
}
