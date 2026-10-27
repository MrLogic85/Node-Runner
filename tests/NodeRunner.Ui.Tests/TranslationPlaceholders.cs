using System.Text;
using System.Text.RegularExpressions;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// Finds translations in a <c>.po</c> file that lose, repeat or add a <c>{0}</c>-style
/// placeholder; docs/LOCALIZATION.md → "Adding a language". <c>msgfmt --check</c> can't,
/// since the template has no format flags.
/// </summary>
internal static partial class TranslationPlaceholders
{
    public const string Folder = "project/locale";

    /// <summary>One message read from a <c>.po</c> file, with one translation per form, like <c>msgstr[1]</c>.</summary>
    private sealed record Message(string? Context, string Id, IReadOnlyList<(string Form, string Text)> Translations);

    /// <summary>A message being read, whose strings may still continue on the next lines.</summary>
    private sealed class Draft
    {
        public StringBuilder? Context;
        public StringBuilder? Id;
        public readonly List<(string Form, StringBuilder Text)> Translations = [];

        public Message ToMessage() =>
            new(Context?.ToString(), Id?.ToString() ?? "", Translations.Select(translation => (translation.Form, translation.Text.ToString())).ToList());
    }

    /// <summary>Each translated form, plural forms too, that doesn't use its msgid's placeholders exactly once each.</summary>
    public static IEnumerable<string> Problems(string file, string po)
    {
        foreach (var message in Read(po).Where(message => message.Id.Length > 0))
        {
            var expected = Placeholders(message.Id).Distinct().Order().ToArray();
            foreach (var (form, text) in message.Translations.Where(translation => translation.Text.Length > 0))
            {
                var used = Placeholders(text).Order().ToArray();
                if (!used.SequenceEqual(expected))
                {
                    var context = message.Context is null ? "" : $" (msgctxt {Quote(message.Context)})";
                    yield return $"{file}: msgid {Quote(message.Id)}{context} {form} uses {Describe(used)}, not {Describe(expected)}";
                }
            }
        }
    }

    // {{ and }} are literal braces; {0,5} and {0:0.0} are placeholder 0.
    [GeneratedRegex(@"\{\{|\}\}|\{(?<index>\d+)(?:[,:][^}]*)?\}")]
    private static partial Regex PlaceholderPattern();

    private static IEnumerable<int> Placeholders(string text) =>
        PlaceholderPattern().Matches(text)
            .Where(match => match.Groups["index"].Success)
            .Select(match => int.Parse(match.Groups["index"].Value));

    private static string Describe(int[] placeholders) =>
        placeholders.Length == 0 ? "none" : string.Concat(placeholders.Select(index => $"{{{index}}}"));

    private static string Quote(string text) => $"\"{text}\"";

    /// <summary>
    /// Reads msgctxt, msgid and msgstr, joining strings wrapped over several lines. Comments,
    /// flags and obsolete (<c>#~</c>) entries are skipped.
    /// </summary>
    private static IEnumerable<Message> Read(string po)
    {
        var messages = new List<Draft>();
        Draft? message = null;
        StringBuilder? field = null;
        foreach (var raw in po.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                field = null;
                continue;
            }

            if (line.StartsWith('"'))
            {
                field?.Append(Unquote(line));
                continue;
            }

            var space = line.IndexOf(' ');
            var keyword = space < 0 ? line : line[..space];
            var text = new StringBuilder(space < 0 ? "" : Unquote(line[(space + 1)..].Trim()));
            field = text;

            // A msgctxt or msgid after a msgstr starts the next message.
            if (keyword is "msgctxt" or "msgid" && (message is null || message.Translations.Count > 0))
            {
                message = new Draft();
                messages.Add(message);
            }

            switch (keyword)
            {
                case "msgctxt":
                    message!.Context = text;
                    break;
                case "msgid":
                    message!.Id = text;
                    break;
                case "msgid_plural":
                    break;
                default:
                    message?.Translations.Add((keyword, text));
                    break;
            }
        }

        return messages.Select(draft => draft.ToMessage());
    }

    private static string Unquote(string quoted)
    {
        var text = new StringBuilder();
        for (var i = 1; i < quoted.Length - 1; i++)
        {
            if (quoted[i] == '\\' && i + 1 < quoted.Length - 1)
            {
                i++;
                text.Append(quoted[i] switch { 'n' => '\n', 't' => '\t', 'r' => '\r', var escaped => escaped });
            }
            else
            {
                text.Append(quoted[i]);
            }
        }

        return text.ToString();
    }
}
