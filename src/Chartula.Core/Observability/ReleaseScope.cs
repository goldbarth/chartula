namespace Chartula.Core.Observability;

/// <summary>
/// How much release a run worked on - the context every token count needs. A run's
/// cost follows the length of the pull request descriptions more than their number,
/// so both are recorded: ten terse pull requests and ten long ones are different runs.
/// </summary>
/// <param name="Commits">Commits in the release range.</param>
/// <param name="PullRequests">Merged pull requests behind those commits.</param>
/// <param name="Facts">Facts the changelog was written from, after filtering.</param>
/// <param name="FactsWithDescription">Facts that carry a description.</param>
/// <param name="DescriptionCharacters">
/// Characters of description across the facts - the text the model reads beyond the
/// titles, after template comments are stripped and at the configured depth.
/// </param>
public sealed record ReleaseScope(
    int Commits,
    int PullRequests,
    int Facts,
    int FactsWithDescription,
    long DescriptionCharacters);
