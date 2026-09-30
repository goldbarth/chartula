namespace Chartula.Core.Observability;

/// <summary>
/// The size of the release a run worked on. Every token count needs this context.
/// A run's cost depends more on the length of the pull request descriptions than on
/// their number, so both are recorded: ten terse pull requests and ten long ones are
/// different runs.
/// </summary>
/// <param name="Commits">Commits in the release range.</param>
/// <param name="PullRequests">Merged pull requests behind those commits.</param>
/// <param name="Facts">Facts the changelog was written from, after filtering.</param>
/// <param name="FactsWithDescription">Facts that carry a description.</param>
/// <param name="DescriptionCharacters">
/// Description characters across all facts: the text the model reads beyond the titles.
/// Counted after template comments are stripped and at the configured depth.
/// </param>
/// <param name="CommitsWithoutPullRequest">
/// Commits that belong to no merged pull request, such as direct pushes. Each became a
/// change of its own, except the merge commits among them.
/// </param>
/// <param name="MergeCommitsSkipped">
/// Merge commits without a pull request, which became no change: what they bring in is
/// in the range already. Counted so that no commit leaves the release unmentioned.
/// </param>
public sealed record ReleaseScope(
    int Commits,
    int PullRequests,
    int Facts,
    int FactsWithDescription,
    long DescriptionCharacters,
    int CommitsWithoutPullRequest = 0,
    int MergeCommitsSkipped = 0);
