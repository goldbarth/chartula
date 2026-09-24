namespace Chartula.Cli.Commands;

/// <summary>
/// Reads where a release starts: <c>--since &lt;ref&gt;</c>, or <c>--whole-history</c>
/// to deliberately render a range that spans all history.
/// A tag with a previous tag needs neither. A first tag needs one of them, because
/// the operator decides where it starts, and Chartula does not guess.
/// </summary>
/// <param name="Since">The tag or commit the release starts after, or <c>null</c>.</param>
/// <param name="WholeHistory">Whether a range over all history may be rendered.</param>
internal sealed record ReleaseStart(string? Since, bool WholeHistory)
{
    public const string SinceOption = "--since";
    public const string WholeHistoryFlag = "--whole-history";

    /// <summary>
    /// The start named on the command line.
    /// Returns false and fills <paramref name="error"/> when <c>--since</c> has no value,
    /// or when both options are given: they answer the same question in two ways.
    /// </summary>
    public static bool TryParse(IReadOnlyList<string> args, out ReleaseStart start, out string? error)
    {
        start = new ReleaseStart(null, false);
        error = null;

        bool wholeHistory = CommandLineArguments.HasFlag(args, WholeHistoryFlag);
        string? since = CommandLineArguments.GetOption(args, SinceOption);
        if (CommandLineArguments.HasFlag(args, SinceOption)
            && (string.IsNullOrWhiteSpace(since) || since.StartsWith("--", StringComparison.Ordinal)))
        {
            error = $"{SinceOption} needs a tag or commit the release starts after.";
            return false;
        }

        if (since is not null && wholeHistory)
        {
            error = $"Pass either {SinceOption} or {WholeHistoryFlag}, not both.";
            return false;
        }

        start = new ReleaseStart(since, wholeHistory);
        return true;
    }

    /// <summary>What a refused whole-history run prints: the cause and both ways out.</summary>
    public static string Refusal(string tag, int commitCount)
        => $"""
            {tag} is the first tag, so its range is the whole history ({commitCount} commits).
              Rendered as it is, that reads as a development log rather than a release.
              {SinceOption} <ref>     start the release after a tag or commit (e.g. the last state you shipped)
              {WholeHistoryFlag}   render all of it, e.g. for a project whose history is the release
            """;
}
