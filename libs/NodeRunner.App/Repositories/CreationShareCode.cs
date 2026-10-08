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

/// <summary>A read share code: the build it holds, or why it was refused.</summary>
public sealed record ShareCodeRead(CreationDef? Build, ShareCodeRefusal? Refusal)
{
    public static ShareCodeRead Refused(ShareCodeRefusal refusal) => new(null, refusal);
}

/// <summary>
/// A Creation's build as one text to copy and paste (#899): <c>NR4.</c> and its <c>creation.json</c>,
/// build only and with a placeholder id, compressed against that format version's share dictionary, in
/// URL-safe base64. It goes through the same migrations as a saved file. docs/SAVE_FORMAT.md →
/// "Share code" owns the format.
/// </summary>
public static class CreationShareCode
{
    /// <summary>The longest code read, white space left out; a longer text is refused as damaged.</summary>
    public const int MaxLength = 32 * 1024;

    /// <summary>The most JSON a code may unpack to, so a small code can't unpack to a huge one.</summary>
    public const int MaxJsonBytes = 1024 * 1024;

    // Every code starts NR, its format version and a dot, as in NR4.
    private const string _start = "NR";
    private const char _versionEnd = '.';

    // Every code's id, which Import replaces; a random one would cost about 35 characters.
    private static readonly Guid _placeholderId = new("00000000-0000-0000-0000-000000000001");

    private static readonly JsonSerializerOptions _compact = new(SaveJson.Options) { WriteIndented = false };

    /// <summary>The code for <paramref name="creation"/>'s build: its name, its parts and their names and settings.</summary>
    public static string Create(CreationDef creation)
    {
        ArgumentNullException.ThrowIfNull(creation);
        return Pack(FileCreationRepository.Format.CurrentVersion, Json(creation));
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

        if (code.Length > MaxLength || Dictionary(version) is not { } dictionary || Unpack(dictionary, body) is not { } json)
        {
            return ShareCodeRead.Refused(ShareCodeRefusal.Damaged);
        }

        CreationDef creation;
        try
        {
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
            : new ShareCodeRead(new CreationDef(creation.Id, name, creation.Creature), null);
    }

    /// <summary>The <c>creation.json</c> a code holds: the build only, with the placeholder id, on one line.</summary>
    internal static byte[] Json(CreationDef creation)
    {
        var file = JsonNode.Parse(FileCreationRepository.Format.Serialize(new CreationDef(_placeholderId, creation.Name, creation.Creature)))!;
        return Encoding.UTF8.GetBytes(file.ToJsonString(_compact));
    }

    /// <summary>
    /// The share dictionary for <paramref name="version"/>, or null when it has none: a creation.json
    /// in that version with every kind of part, so a code only holds where its build differs from it.
    /// One is added with each new format version and never changed, or older codes would no longer read.
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
    internal static string Pack(int version, byte[] json)
    {
        var dictionary = Dictionary(version)
            ?? throw new InvalidOperationException($"Format version {version} has no share dictionary ({DictionaryName(version)}).");
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

    // Splits NR4.body into its version and body.
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
