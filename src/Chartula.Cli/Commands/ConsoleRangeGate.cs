using Chartula.Core.History;
using Chartula.Core.Pipeline;

namespace Chartula.Cli.Commands;

/// <summary>
/// Shows the range in the run header and asks before a large one is read.
/// It asks twice, with no as the default, because a yes here is the one step between a
/// forgotten <c>--since</c> and a paid run over every commit up to the tag.
/// A preview sends nothing to the model, so it asks once, about the GitHub requests.
/// Without a terminal nobody can answer, so it declines and names <c>--yes</c>.
/// </summary>
/// <param name="interactive">Whether a person can answer on <paramref name="input"/>.</param>
internal sealed class ConsoleRangeGate(TextReader input, TextWriter output, bool interactive, LargeRangeRule rule)
    : IReleaseRangeGate
{
    public const string YesFlag = "--yes";

    // The column the values of "Model:" and "GitHub:" start on, which is also the one
    // the step labels below the header start on (#351).
    private const string Indent = "         ";

    public void Announce(CommitRange range) => output.WriteLine($"Range:   {Describe(range)}");

    public async Task<bool> ConfirmAsync(
        CommitRange range, bool sendsToModel = true, CancellationToken cancellationToken = default)
    {
        int commits = range.Commits.Count;
        if (!range.StartsAtFirstCommit)
        {
            output.WriteLine($"{Indent}That is more than range.confirmAboveCommits ({rule.CommitThreshold}).");
        }

        output.WriteLine(sendsToModel
            ? $"{Indent}It costs {Count(commits, "GitHub request")}, and every pull request in it goes to the model once per audience."
            : $"{Indent}It costs {Count(commits, "GitHub request")}. A preview sends nothing to the model.");
        output.WriteLine(
            $"{Indent}To start later, pass {ReleaseStart.SinceOption} <ref>: the commits after <ref>, up to {range.ToTag}.");

        if (!interactive)
        {
            output.WriteLine($"{Indent}No terminal to confirm this. Pass {YesFlag} to confirm it up front.");
            return false;
        }

        if (!sendsToModel)
        {
            return await AskAsync($"Read all {Count(commits, "commit")} up to {range.ToTag}? [y/N] ", cancellationToken);
        }

        return await AskAsync($"Render all {Count(commits, "commit")} up to {range.ToTag}? [y/N] ", cancellationToken)
               && await AskAsync("This sends every pull request in the range to the model. Continue? [y/N] ", cancellationToken);
    }

    /// <summary>The range with both of its ends, as the header shows it.</summary>
    internal static string Describe(CommitRange range)
        => range.StartsAtFirstCommit
            ? $"every commit up to {range.ToTag} ({Count(range.Commits.Count, "commit")}), the first tag"
            : $"the commits after {range.From}, up to {range.ToTag} ({Count(range.Commits.Count, "commit")})";

    private async Task<bool> AskAsync(string question, CancellationToken cancellationToken)
    {
        output.Write(question);
        string? answer = (await input.ReadLineAsync(cancellationToken))?.Trim();
        return answer is not null
               && (answer.Equals("y", StringComparison.OrdinalIgnoreCase)
                   || answer.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }

    private static string Count(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count:N0} {noun}s";
}
