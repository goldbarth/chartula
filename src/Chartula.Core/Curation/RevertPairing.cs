using System.Text.RegularExpressions;
using Chartula.Core.History;
using Chartula.Core.PullRequests;

namespace Chartula.Core.Curation;

/// <summary>
/// Takes a revert and what it reverted out of a release when both are in it: the
/// changes never shipped, and a revert filtered away as internal would leave them in
/// the facts as if they had (#206). Pairing is a fact decision, so it only follows
/// what a revert names in a form read without judgment:
/// <list type="bullet">
/// <item>a commit hash of at least 7 characters that is the prefix of exactly one
/// commit in the range ("This reverts commit ...", "Reverts d85a4b9, ...");</item>
/// <item>GitHub's revert button: "Reverts owner/repo#N" or "Reverts #N".</item>
/// </list>
/// A pull request number in prose is not a target: "#61" is as often context. A
/// revert whose every named target is in the release drops out with them; one that
/// names anything it cannot resolve here - an earlier release, an ambiguous hash -
/// takes back what it did resolve and stays, so the reader still sees it. A revert
/// that is itself reverted in the same release is undone, one level deep: its
/// targets stay.
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

        // A revert another revert takes back no longer takes anything back itself.
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
        // The body as a reader sees it: a hash in a hidden template comment names nothing.
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
