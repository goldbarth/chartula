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

internal sealed class StubCommitReader(DateOnly? taggedAt = null) : IReleaseCommitReader
{
    /// <summary>The date the stubbed tag was made, when a test does not name one.</summary>
    public static readonly DateOnly DefaultTagDate = new(2026, 7, 17);

    private readonly DateOnly? _taggedAt = taggedAt ?? DefaultTagDate;

    public Task<CommitRange> ReadReleaseCommitsAsync(string tag, CancellationToken cancellationToken = default)
        => Task.FromResult(new CommitRange(tag, null, [new CommitInfo("sha", "feat: add search")], _taggedAt));
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
        => Task.FromResult(FaithfulnessReport.Checked([]));
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

internal sealed class SpyCustomerPageWriter : ICustomerPageWriter
{
    public int Calls { get; private set; }

    public CustomerPage? LastPage { get; private set; }

    public Task<string> WriteAsync(CustomerPage page, CancellationToken cancellationToken = default)
    {
        Calls++;
        LastPage = page;
        return Task.FromResult("release-" + page.Tag + ".md");
    }
}

/// <summary>A renderer whose customer rendering can be steered by a test.</summary>
internal sealed class CustomerRenderer(string text, string? description = null) : IReleaseRenderer
{
    public Task<IReadOnlyDictionary<Audience, ChangelogGenerationResult>> RenderAllAsync(
        FactBase factBase, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<Audience, ChangelogGenerationResult>>(
            new Dictionary<Audience, ChangelogGenerationResult>
            {
                [Audience.Technical] = ChangelogGenerationResult.Success("- Added search"),
                [Audience.Customer] = ChangelogGenerationResult.Success(text, description),
                [Audience.Product] = ChangelogGenerationResult.Success("- Search shipped."),
            });
}

/// <summary>A thorough check that records the text it was handed, and finds nothing.</summary>
internal sealed class RecordingThoroughChecker : IThoroughFaithfulnessChecker
{
    public List<string> Checked { get; } = [];

    public Task<FaithfulnessReport> CheckAsync(
        string output, FactBase factBase, CancellationToken cancellationToken = default)
    {
        Checked.Add(output);
        return Task.FromResult(FaithfulnessReport.Checked([]));
    }
}
