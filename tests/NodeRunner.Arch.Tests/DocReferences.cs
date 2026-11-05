using System.Text.RegularExpressions;

namespace NodeRunner.Arch.Tests;

/// <summary>
/// Checks that agent files name only existing docs, headings and sections: docs/REVIEW.md → "Agent,
/// rule and skill files". A renamed heading would otherwise leave the link stale.
/// </summary>
internal static partial class DocReferences
{
    private const string _howToWrite = "write `docs/X.md` → \"Heading\" or `docs/X.md` §N";

    // The examples' placeholder docs, such as docs/X.md and docs/release-notes/X.Y.Z.md.
    private const string _placeholder = "X.";

    private static readonly string[] _folders = [".claude/skills", ".claude/agents", ".claude/rules"];

    /// <summary>
    /// Every doc reference under <paramref name="root"/> to a missing doc, heading or section, or to a
    /// subsection.
    /// </summary>
    internal static List<string> Problems(string root)
    {
        var problems = new List<string>();
        var headings = new Dictionary<string, string[]?>(StringComparer.Ordinal);
        foreach (var path in AgentFiles.Instructions(root, _folders))
        {
            var shown = Path.GetRelativePath(root, path).Replace('\\', '/');

            // Joins wrapped lines, so a reference split over two lines reads as one.
            var text = Whitespace().Replace(File.ReadAllText(path), " ");
            foreach (Match reference in Reference().Matches(text))
            {
                var doc = reference.Groups["doc"].Value;
                if (Path.GetFileName(doc).StartsWith(_placeholder, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!headings.TryGetValue(doc, out var known))
                {
                    var file = Path.Combine(root, doc);
                    known = File.Exists(file) ? Headings(file) : null;
                    headings[doc] = known;
                }

                var quoted = reference.Value.Trim();
                if (known is null)
                {
                    problems.Add($"{shown}: {quoted} names a doc that does not exist.");
                    continue;
                }

                var named = reference.Groups["heading"].Captures.Select(capture => capture.Value).ToList();
                var section = reference.Groups["section"].Value;
                var subsection = Subsection().Match(text, reference.Index + reference.Length);
                if (subsection.Success)
                {
                    problems.Add($"{shown}: {quoted}{subsection.Value} names a subsection, which is no heading; {_howToWrite}.");
                }

                foreach (var heading in named.Where(heading => !known.Contains(heading)))
                {
                    problems.Add($"{shown}: {quoted} names \"{heading}\", which is no heading in {doc}; {_howToWrite}.");
                }

                if (section.Length > 0 && !known.Any(heading => heading.StartsWith($"{section}. ", StringComparison.Ordinal)))
                {
                    problems.Add($"{shown}: {quoted} names §{section}, which is no \"{section}. …\" heading in {doc}; {_howToWrite}.");
                }
            }
        }

        return problems;
    }

    /// <summary>The doc's headings, without the comment lines of its code blocks.</summary>
    private static string[] Headings(string file)
    {
        var headings = new List<string>();
        var inCode = false;
        foreach (var line in File.ReadAllLines(file))
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inCode = !inCode;
                continue;
            }

            var match = HeadingLine().Match(line);
            if (!inCode && match.Success)
            {
                headings.Add(match.Groups[1].Value.Trim());
            }
        }

        return [.. headings];
    }

    // The section number Reference leaves unread, such as the "§9.2" of a subsection.
    [GeneratedRegex(@"\G §\d+(?:\.\d+)+")]
    private static partial Regex Subsection();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"^#{1,6} (.+)$")]
    private static partial Regex HeadingLine();

    // A doc or agent file, then "→" and one or more quoted headings ("A", "B" and "C"), or a § section number;
    // a subsection such as §9.2 is no section, so Subsection reads it.
    [GeneratedRegex("""`(?<doc>(?:docs/[\w./-]+|\.claude/agents/[\w-]+|README|AGENTS)\.md)`(?: → "(?<heading>[^"]+)"(?:(?:, | and | or )"(?<heading>[^"]+)")*| §(?<section>\d+)(?!\d|\.\d))?""")]
    private static partial Regex Reference();
}
