using System.Text.RegularExpressions;
using Chartula.Core.History;
using Chartula.Core.PullRequests;

namespace Chartula.Core.Curation;

/// <summary>
/// Removes a revert and its reverted change from a release when both are in it.
/// Neither change shipped. If the revert were filtered out as internal, the reverted
/// change would stay in the facts as if it had shipped (#206).
/// Pairing is a fact decision, so it only follows targets a revert names unambiguously:
/// <list type="bullet">
/// <item>a commit hash of at least 7 characters that is the prefix of exactly one
/// commit in the range ("This reverts commit ...", "Reverts d85a4b9, ...");</item>
/// <item>GitHub's revert button: "Reverts owner/repo#N" or "Reverts #N".</item>
/// </list>
/// A pull request number in prose is not a target, because "#61" is as often just context.
/// Outcomes:
/// <list type="bullet">
/// <item>All named targets are in the release: the revert is removed together with them.</item>
/// <item>A named target cannot be resolved (an earlier release, an ambiguous hash): the
/// resolved targets are removed, and the revert stays so the reader still sees it.</item>
/// <item>A revert that is itself reverted in the same release is undone, one level
/// deep: its targets stay.</item>
/// </list>
/// </summary>
internal static partial class RevertPairing
{
    // "revert: ...", "revert(scope)!: ..." or GitHub's 'Revert "<title>"'.
    [GeneratedRegex(@"^\s*(?:revert(?:\([^)]*\))?!?:|revert\s+"")", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RevertTitle();

    // A hex word with at least one digit, so an all-letter word like "defaced" is not a hash.
    [GeneratedRegex(@"\b(?=[0-9a-f]*[0-9])[0-9a-f]{7,40}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Hash();

    [GeneratedRegex(@"\breverts\s+(?:[\w.-]+/[\w.-]+)?#(?<number>\d+)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RevertsPullRequest();

    /// <summary>The pull requests that remain once paired reverts and their targets are taken out.</summary>
    public static IReadOnlyList<PullRequestInfo> Apply(CommitRange range, IReadOnlyList<PullRequestInfo> pulls)
    {
        Dictionary<string, int> pullByCommit = new(StringComparer.OrdinalIgnoreCase);
        foreach (PullRequestInfo pull in pulls)
        {
            foreach (string sha in pull.CommitShas)
            {
                pullByCommit.TryAdd(sha, pull.Number);
            }
        }

        HashSet<int> numbers = [.. pulls.Select(pull => pull.Number)];
        Dictionary<int, (IReadOnlyList<int> Targets, bool Complete)> reverts = [];
        foreach (PullRequestInfo pull in pulls.Where(pull => RevertTitle().IsMatch(pull.Title)))
        {
            reverts[pull.Number] = Resolve(pull, range, pullByCommit, numbers);
        }

        // A revert that another revert takes back removes nothing itself.
        HashSet<int> undone = [.. reverts.Values.SelectMany(revert => revert.Targets).Where(reverts.ContainsKey)];

        HashSet<int> removed = [];
        foreach ((int number, (IReadOnlyList<int> targets, bool complete)) in reverts)
        {
            if (undone.Contains(number))
            {
                continue;
            }

            removed.UnionWith(targets);
            if (complete && targets.Count > 0)
            {
                removed.Add(number);
            }
        }

        return [.. pulls.Where(pull => !removed.Contains(pull.Number))];
    }

    private static (IReadOnlyList<int> Targets, bool Complete) Resolve(
        PullRequestInfo revert, CommitRange range, Dictionary<string, int> pullByCommit, HashSet<int> numbers)
    {
        // Read the body as a reader sees it: a hash in a hidden template comment names nothing.
        string text = revert.Title + "\n" + PullRequestBody.Description(revert.Description);

        List<int> targets = [];
        bool complete = true;
        foreach (Match hash in Hash().Matches(text))
        {
            List<string> matching = [.. range.Commits
                .Select(commit => commit.Sha)
                .Where(sha => sha.StartsWith(hash.Value, StringComparison.OrdinalIgnoreCase))];

            if (matching.Count == 1 && pullByCommit.TryGetValue(matching[0], out int target) && target != revert.Number)
            {
                targets.Add(target);
            }
            else
            {
                complete = false;
            }
        }

        foreach (Match reference in RevertsPullRequest().Matches(text))
        {
            int target = int.Parse(reference.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (numbers.Contains(target) && target != revert.Number)
            {
                targets.Add(target);
            }
            else
            {
                complete = false;
            }
        }

        return ([.. targets.Distinct()], complete);
    }
}
