namespace Chartula.Cli.Commands;

/// <summary>
/// Reads where a release starts: <c>--since &lt;ref&gt;</c>.
/// Without it, a release starts after the previous tag, or at the first commit for a
/// first tag. Either way it ends at the release tag.
/// </summary>
/// <param name="Since">The tag or commit the release starts after, or <c>null</c>.</param>
internal sealed record ReleaseStart(string? Since)
{
    public const string SinceOption = "--since";

    /// <summary>
    /// The start named on the command line.
    /// Returns false and fills <paramref name="error"/> when <c>--since</c> has no value.
    /// </summary>
    public static bool TryParse(IReadOnlyList<string> args, out ReleaseStart start, out string? error)
    {
        start = new ReleaseStart(Since: null);
        error = null;

        string? since = CommandLineArguments.GetOption(args, SinceOption);
        if (CommandLineArguments.HasFlag(args, SinceOption)
            && (string.IsNullOrWhiteSpace(since) || since.StartsWith("--", StringComparison.Ordinal)))
        {
            error = $"{SinceOption} needs a tag or commit the release starts after.";
            return false;
        }

        start = new ReleaseStart(since);
        return true;
    }
}
