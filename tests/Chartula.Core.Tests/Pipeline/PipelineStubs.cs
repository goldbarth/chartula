using Chartula.Core.Facts;
using Chartula.Core.Faithfulness;
using Chartula.Core.Generation;
using Chartula.Core.History;
using Chartula.Core.Llm;
using Chartula.Core.PullRequests;
using Chartula.Core.Releases;
using Chartula.Core.Rendering;
using Chartula.Core.Review;
using Chartula.Core.Serialization;

namespace Chartula.Core.Tests.Pipeline;

internal sealed class StubCommitReader : IReleaseCommitReader
{
    public Task<CommitRange> ReadReleaseCommitsAsync(string tag, CancellationToken cancellationToken = default)
        => Task.FromResult(new CommitRange(tag, null, [new CommitInfo("sha", "feat: add search")]));
}

internal sealed class StubPullRequestReader : IReleasePullRequestReader
{
    public Task<IReadOnlyList<PullRequestInfo>> GetMergedPullRequestsAsync(
        RepositoryCoordinates repository, CommitRange range, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PullRequestInfo>>(
            [new PullRequestInfo(7, "feat: add search", "Adds search.", [], "https://example/pull/7")]);
}

internal sealed class StubRenderer : IReleaseRenderer
{
    public Task<IReadOnlyDictionary<Audience, ChangelogGenerationResult>> RenderAllAsync(
        FactBase factBase, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<Audience, ChangelogGenerationResult>>(
            new Dictionary<Audience, ChangelogGenerationResult>
            {
                [Audience.Technical] = ChangelogGenerationResult.Success("- Added search"),
                [Audience.Customer] = ChangelogGenerationResult.Success("- Search is here."),
                [Audience.Product] = ChangelogGenerationResult.Success("- Search shipped."),
            });
}

internal sealed class PassThroughThoroughChecker : IThoroughFaithfulnessChecker
{
    public Task<FaithfulnessReport> CheckAsync(
        string output, FactBase factBase, CancellationToken cancellationToken = default)
        => Task.FromResult(new FaithfulnessReport(true, []));
}

/// <summary>A writer spy that records whether it was called.</summary>
internal sealed class SpyJsonWriter : IChangelogJsonWriter
{
    public int Calls { get; private set; }

    public Task<string> WriteAsync(
        FactBase factBase,
        IReadOnlyDictionary<Audience, string>? renderings = null,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult("changelog.json");
    }
}

internal sealed class SpyMarkdownWriter : IChangelogMarkdownWriter
{
    public int Calls { get; private set; }

    public Task<string> WriteAsync(string tag, string body, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult("CHANGELOG.md");
    }
}

internal sealed class SpyReleaseNotesWriter : IReleaseNotesWriter
{
    public int Calls { get; private set; }

    public Task<string> WriteAsync(
        RepositoryCoordinates repository, string tag, string body, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult("https://github.com/octo/repo/releases/tag/" + tag);
    }
}
