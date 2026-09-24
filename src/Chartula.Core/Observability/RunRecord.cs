using Chartula.Core.History;
using Chartula.Core.Pipeline;
using Chartula.Core.PullRequests;

namespace Chartula.Core.Observability;

/// <summary>
/// What a run did and cost, kept in a file after the terminal output is gone.
/// Two runs can then be compared from their files instead of numbers copied by hand.
/// It holds no rendered text: the texts are in <c>changelog.json</c>, and this record
/// is about the run, not the release.
/// </summary>
/// <param name="Tag">The release tag the run was for.</param>
/// <param name="Repository">The repository the release belongs to.</param>
/// <param name="Mode">How the run treated its outputs.</param>
/// <param name="Audiences">One outcome per audience the run asked for, in order.</param>
/// <param name="Metrics">Calls, tokens and check activity, as the run summary prints them.</param>
public sealed record RunRecord(
    string Tag,
    RepositoryCoordinates Repository,
    PipelineMode Mode,
    IReadOnlyList<AudienceOutcome> Audiences,
    RunReport Metrics)
{
    /// <summary>
    /// The commits the run read, or <c>null</c> when they are not known.
    /// Without it, a run cannot be told apart from a wrong range: the same tag over other
    /// commits is another release, with other facts and other flags.
    /// </summary>
    public CommitRange? Range { get; init; }

    /// <summary>
    /// The start the operator named, or <c>null</c> when none was named.
    /// It says whether <see cref="CommitRange.From"/> was named or found as the previous tag.
    /// </summary>
    public string? Since { get; init; }
}
