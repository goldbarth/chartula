using Chartula.Cli.Terminal;

namespace Chartula.Cli.Commands;

/// <summary>
/// Reads <c>--name value</c> options and <c>--name</c> flags, after <see cref="Check"/>
/// has confirmed that every argument is one the command takes.
/// The readers look options up by name, so on their own they would pass over anything
/// they do not know: a misspelled <c>--tag</c> ran for another release, a misspelled
/// <c>--no-publish</c> published, and <c>generate --help</c> started a run (#300).
/// So the command line is refused, not read, when any part of it cannot be read as meant.
/// </summary>
internal static class CommandLineArguments
{
    private const string Tag = "--tag";
    private const string Repo = "--repo";
    private const string Audience = "--audience";
    private const string NoPublish = "--no-publish";
    internal const string ReplacePublished = "--replace-published";

    /// <summary>What each value option takes, as the error for a missing value names it.</summary>
    private static readonly Dictionary<string, string> ValueOptions = new(StringComparer.Ordinal)
    {
        [Tag] = "<release-tag>",
        [Repo] = "<owner/name>",
        [ReleaseStart.SinceOption] = "<ref>",
        [Audience] = "<technical|customer|product>",
    };

    /// <summary>The one option that may be repeated: each repetition names another audience.</summary>
    private static readonly HashSet<string> Repeatable = new(StringComparer.Ordinal) { Audience };

    /// <summary>
    /// The options each command takes. <c>doctor</c> checks the setup for a release and
    /// reads no range; only <c>generate</c> publishes.
    /// </summary>
    private static readonly Dictionary<string, string[]> Commands = new(StringComparer.Ordinal)
    {
        ["preview"] = [Tag, Repo, ReleaseStart.SinceOption, Audience, ConsoleRangeGate.YesFlag, TerminalProfile.PlainFlag],
        ["generate"] = [Tag, Repo, ReleaseStart.SinceOption, Audience, ConsoleRangeGate.YesFlag, NoPublish, ReplacePublished, TerminalProfile.PlainFlag],
        [DoctorCommand.Name] = [Tag, Repo, TerminalProfile.PlainFlag],
    };

    /// <summary>Whether <paramref name="command"/> is one of the commands the CLI runs.</summary>
    public static bool IsCommand(string command) => Commands.ContainsKey(command);

    /// <summary>
    /// Whether <paramref name="arg"/> asks for the help. It is honoured wherever it
    /// stands, so <c>generate --help</c> prints the help instead of starting a run.
    /// </summary>
    public static bool IsHelp(string arg) => arg is "-h" or "--help";

    /// <summary>
    /// Why the arguments after the command cannot be read as meant, or <c>null</c> when
    /// every one of them is an option the command takes, with a value where it needs one.
    /// </summary>
    /// <param name="args">The command line, the command first.</param>
    public static string? Check(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string command = args[0];
        string[] accepted = Commands[command];
        HashSet<string> seen = new(StringComparer.Ordinal);

        for (int i = 1; i < args.Count; i++)
        {
            string arg = args[i];
            if (!accepted.Contains(arg, StringComparer.Ordinal))
            {
                return Unaccepted(command, arg, accepted);
            }

            if (!seen.Add(arg) && ValueOptions.ContainsKey(arg) && !Repeatable.Contains(arg))
            {
                return $"{arg} is given twice. Pass it once.";
            }

            if (ValueOptions.TryGetValue(arg, out string? placeholder))
            {
                // A value that is itself an option means the value was left out, and the
                // next option would be read as it: --tag --no-publish named a tag "--no-publish".
                if (i + 1 >= args.Count || args[i + 1].StartsWith('-'))
                {
                    return $"{arg} needs a value: {arg} {placeholder}.";
                }

                i++;
            }
        }

        return null;
    }

    private static string Unaccepted(string command, string arg, string[] accepted)
    {
        if (arg == "--whole-history")
        {
            return "--whole-history is gone: a first tag renders every commit up to it without a flag. " +
                   "To start later, pass --since <ref>.";
        }

        if (!arg.StartsWith('-'))
        {
            return $"Unexpected argument '{arg}'. {command} takes options only, such as {Tag} <release-tag>.";
        }

        int equals = arg.IndexOf('=', StringComparison.Ordinal);
        if (equals > 0 && accepted.Contains(arg[..equals], StringComparer.Ordinal))
        {
            return $"Write {arg[..equals]} {arg[(equals + 1)..]}, with a space, not {arg}.";
        }

        string[] others = [.. Commands.Where(entry => entry.Value.Contains(arg, StringComparer.Ordinal)).Select(entry => entry.Key)];
        if (others.Length > 0)
        {
            return $"{arg} is an option of {string.Join(" and ", others)}, not of {command}.";
        }

        string? closest = accepted
            .Select(option => (Option: option, Distance: Distance(arg, option)))
            .Where(candidate => candidate.Distance <= 2)
            .OrderBy(candidate => candidate.Distance)
            .Select(candidate => candidate.Option)
            .FirstOrDefault();
        return closest is null
            ? $"Unknown option '{arg}' for {command}."
            : $"Unknown option '{arg}' for {command}. Did you mean {closest}?";
    }

    // Levenshtein distance: a typo is one or two edits away from the option it meant.
    private static int Distance(string a, string b)
    {
        int[] previous = [.. Enumerable.Range(0, b.Length + 1)];
        for (int i = 1; i <= a.Length; i++)
        {
            int[] current = new int[b.Length + 1];
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int substitution = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                current[j] = Math.Min(substitution, Math.Min(previous[j] + 1, current[j - 1] + 1));
            }

            previous = current;
        }

        return previous[b.Length];
    }

    /// <summary>Reads the value following <paramref name="name"/>, or <c>null</c>.</summary>
    public static string? GetOption(IReadOnlyList<string> args, string name)
    {
        for (int i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.Ordinal))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    /// <summary>
    /// Every value given for <paramref name="name"/>, whether the option was repeated
    /// or given once with comma-separated values.
    /// Both styles are common, and neither is worth making a person remember.
    /// An empty value, as a trailing comma leaves, is kept: whether it is an error is for
    /// the option to say, and dropped here it would be accepted in silence (#353).
    /// </summary>
    public static IReadOnlyList<string> GetOptions(IReadOnlyList<string> args, string name)
    {
        List<string> values = [];
        for (int i = 0; i < args.Count - 1; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.Ordinal))
            {
                continue;
            }

            values.AddRange(args[i + 1].Split(',', StringSplitOptions.TrimEntries));
        }

        return values;
    }

    /// <summary>Whether <paramref name="name"/> is present as a flag.</summary>
    public static bool HasFlag(IReadOnlyList<string> args, string name)
    {
        for (int i = 0; i < args.Count; i++)
        {
            if (string.Equals(args[i], name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
