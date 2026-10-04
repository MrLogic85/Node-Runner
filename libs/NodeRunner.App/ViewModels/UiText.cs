using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// UI text a view-model hands to Godot, which translates it (#682). <see cref="Message"/> is the
/// English source text and also the gettext msgid; <see cref="Plural"/> is its English plural for
/// <see cref="Count"/>, picked per language by Godot's <c>TrN</c>. <c>{0}</c>, <c>{1}</c>… stand
/// for <see cref="Args"/>: numbers, strings shown as they are (user or data content, never
/// translated), or a nested <see cref="UiText"/> for a phrase that stands on its own. App never
/// renders the English itself, so tests compare <see cref="UiText"/> values.
/// </summary>
public sealed partial class UiText : IEquatable<UiText>
{
    private readonly object[] _args;

    private UiText(string message, string? plural, int count, string? context, object[] args)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        if (context is { Length: 0 })
        {
            throw new ArgumentException("A context can't be empty.", nameof(context));
        }

        foreach (var arg in args)
        {
            if (arg is not (int or long or string or UiText))
            {
                throw new ArgumentException($"Unsupported argument {arg?.GetType().Name ?? "null"}: use int, long, string or UiText.", nameof(args));
            }
        }

        RequirePlaceholders(message, args.Length, nameof(message));
        if (plural is not null)
        {
            RequirePlaceholders(plural, args.Length, nameof(plural));
        }

        Message = message;
        Plural = plural;
        Count = count;
        Context = context;
        _args = args;
    }

    public string Message { get; }

    /// <summary>The English plural of <see cref="Message"/>, or null when the text is not counted.</summary>
    public string? Plural { get; }

    public int Count { get; }

    /// <summary>The gettext context that tells two equal messages apart, or null.</summary>
    public string? Context { get; }

    public IReadOnlyList<object> Args => _args;

    /// <summary>Fixed text with no arguments.</summary>
    public static UiText Plain(string message) => new(message, null, 0, null, []);

    /// <summary>Text with <c>{0}</c>, <c>{1}</c>… filled from <paramref name="args"/>.</summary>
    public static UiText Format(string message, params object[] args) => new(message, null, 0, null, [.. args]);

    /// <summary>
    /// Counted text: <paramref name="count"/> picks the plural form and is also <c>{0}</c>;
    /// <paramref name="args"/> follow as <c>{1}</c>, <c>{2}</c>…. Write whole sentences
    /// ("Trained {0} generation" / "Trained {0} generations"), not a counted phrase inside one.
    /// </summary>
    public static UiText Counted(string singular, string plural, int count, params object[] args)
    {
        ArgumentException.ThrowIfNullOrEmpty(plural);
        return new(singular, plural, count, null, [count, .. args]);
    }

    /// <summary>The same text under a gettext context, for two equal messages that translate differently.</summary>
    public UiText InContext(string context) => new(Message, Plural, Count, context, _args);

    public bool Equals(UiText? other) =>
        other is not null
        && Message == other.Message
        && Plural == other.Plural
        && Count == other.Count
        && Context == other.Context
        && _args.SequenceEqual(other._args);

    public override bool Equals(object? obj) => Equals(obj as UiText);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Message);
        hash.Add(Plural);
        hash.Add(Count);
        hash.Add(Context);
        foreach (var arg in _args)
        {
            hash.Add(arg);
        }

        return hash.ToHashCode();
    }

    /// <summary>A debug view of the parts, not the English text.</summary>
    public override string ToString()
    {
        var text = new StringBuilder();
        text.Append('"').Append(Message).Append('"');
        if (Plural is not null)
        {
            text.Append(" / \"").Append(Plural).Append("\" #").Append(Count);
        }

        if (Context is not null)
        {
            text.Append(" @").Append(Context);
        }

        if (_args.Length > 0)
        {
            text.Append(" (").AppendJoin(", ", _args.Select(arg => arg is string value ? $"'{value}'" : arg.ToString())).Append(')');
        }

        return text.ToString();
    }

    // Every argument is used, so a template and its plural can't drop or invent one, and the only
    // braces are bare {N} placeholders or escaped {{ and }}, so string.Format never fails on the
    // English text.
    private static void RequirePlaceholders(string template, int argCount, string parameter)
    {
        // Read left to right, as string.Format does: an escaped {{ or }}, a {N} placeholder, or a stray brace.
        var braces = Brace().Matches(template);
        var used = braces.Where(brace => brace.Groups[1].Success).Select(brace => int.Parse(brace.Groups[1].Value, CultureInfo.InvariantCulture)).ToHashSet();
        var stray = braces.Any(brace => brace.Length == 1);
        if (stray || !used.SetEquals(Enumerable.Range(0, argCount)))
        {
            throw new ArgumentException($"\"{template}\" must use all {argCount} of its arguments as {{0}}, {{1}}…, with no format.", parameter);
        }
    }

    [GeneratedRegex(@"\{\{|\}\}|\{(\d+)\}|[{}]")]
    private static partial Regex Brace();
}
