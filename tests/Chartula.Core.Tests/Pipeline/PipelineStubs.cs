using Chartula.Core.Facts;
using Chartula.Core.Faithfulness;
using Chartula.Core.Generation;
using Chartula.Core.History;
using Chartula.Core.Llm;
using Chartula.Core.Observability;
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

    /// <summary>What the pipeline passed as the release start.</summary>
    public string? Since { get; private set; }

    /// <summary>
    /// A range bounded by a previous tag, as for any release after the first.
    /// A test about the first release sets <see cref="FirstTag"/>.
    /// </summary>
    public bool FirstTag { get; init; }

    /// <summary>How many commits the range holds.</summary>
    public int CommitCount { get; init; } = 1;

    public Task<CommitRange> ReadReleaseCommitsAsync(
        string tag, string? since = null, CancellationToken cancellationToken = default)
    {
        Since = since;
        string? from = since ?? (FirstTag ? null : "v0.9.0");
        IReadOnlyList<CommitInfo> commits =
            [.. Enumerable.Range(0, CommitCount).Select(static n => new CommitInfo($"sha{n}", "feat: add search"))];
        return Task.FromResult(new CommitRange(tag, from, commits, _taggedAt));
    }
}

internal sealed class StubPullRequestReader : IReleasePullRequestReader
{
    public Task<IReadOnlyList<PullRequestInfo>> GetMergedPullRequestsAsync(
        RepositoryCoordinates repository, CommitRange range, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PullRequestInfo>>(
            [new PullRequestInfo(7, "feat: add search", "Adds search.", [], "https://example/pull/7")
            {
                CommitShas = [.. range.Commits.Select(static commit => commit.Sha)],
            }]);
}

internal sealed class StubRenderer : IReleaseRenderer
{
    public IReadOnlyDictionary<Audience, RenderPlan> Plan(FactBase factBase, IReadOnlyCollection<Audience>? audiences = null)
        => StubPlans.Empty(audiences);

    /// <summary>The audiences the pipeline asked for, or null when it asked for all.</summary>
    public IReadOnlyCollection<Audience>? Asked { get; private set; }

    public int Calls { get; private set; }

    public Task<IReadOnlyDictionary<Audience, ChangelogGenerationResult>> RenderAsync(
        FactBase factBase,
        IReadOnlyCollection<Audience>? audiences = null,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        Asked = audiences;
        Dictionary<Audience, ChangelogGenerationResult> all = new()
        {
            [Audience.Technical] = ChangelogGenerationResult.Success("- Added search"),
            [Audience.Customer] = ChangelogGenerationResult.Success("- Search is here."),
            [Audience.Product] = ChangelogGenerationResult.Success("- Search shipped."),
        };

        // Return only the requested audiences, like the real renderer. Outputs are
        // written from what comes back, so a stub that returned everything would hide
        // the behaviour under test.
        return Task.FromResult<IReadOnlyDictionary<Audience, ChangelogGenerationResult>>(
            audiences is null ? all : all.Where(e => audiences.Contains(e.Key)).ToDictionary());
    }
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

    public Task<string> WriteAsync(
        string tag, DateOnly? taggedAt, string body, CancellationToken cancellationToken = default)
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

    public int Checks { get; private set; }

    public Task EnsureCanWriteAsync(
        RepositoryCoordinates repository, string tag, CancellationToken cancellationToken = default)
    {
        Checks++;
        return Task.CompletedTask;
    }
}

/// <summary>
/// A release notes writer that passes the check and then fails on writing, like a
/// release GitHub refuses for a reason the check cannot see.
/// </summary>
internal sealed class RefusingReleaseNotesWriter(string message) : IReleaseNotesWriter
{
    public Task<string> WriteAsync(
        RepositoryCoordinates repository, string tag, string body, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(message);

    public Task EnsureCanWriteAsync(
        RepositoryCoordinates repository, string tag, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>A release notes writer whose token may not publish, found by the check.</summary>
internal sealed class ReadOnlyTokenReleaseNotesWriter(string message) : IReleaseNotesWriter
{
    public int Calls { get; private set; }

    public Task<string> WriteAsync(
        RepositoryCoordinates repository, string tag, string body, CancellationToken cancellationToken = default)
    {
        Calls++;
        throw new InvalidOperationException(message);
    }

    public Task EnsureCanWriteAsync(
        RepositoryCoordinates repository, string tag, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(message);
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
/// <summary>Renders all three audiences, failing the ones named.</summary>
internal sealed class FailingRenderer(params Audience[] failing) : IReleaseRenderer
{
    public IReadOnlyDictionary<Audience, RenderPlan> Plan(FactBase factBase, IReadOnlyCollection<Audience>? audiences = null)
        => StubPlans.Empty(audiences);

    public Task<IReadOnlyDictionary<Audience, ChangelogGenerationResult>> RenderAsync(
        FactBase factBase,
        IReadOnlyCollection<Audience>? audiences = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<Audience, ChangelogGenerationResult>>(
            new[] { Audience.Technical, Audience.Customer, Audience.Product }.ToDictionary(
                audience => audience,
                audience => failing.Contains(audience)
                    ? ChangelogGenerationResult.Failure("Status Code: Unauthorized")
                    : ChangelogGenerationResult.Success($"- {audience} text")));
}

internal sealed class CustomerRenderer(string text, string? description = null) : IReleaseRenderer
{
    public IReadOnlyDictionary<Audience, RenderPlan> Plan(FactBase factBase, IReadOnlyCollection<Audience>? audiences = null)
        => StubPlans.Empty(audiences);

    public Task<IReadOnlyDictionary<Audience, ChangelogGenerationResult>> RenderAsync(
        FactBase factBase,
        IReadOnlyCollection<Audience>? audiences = null,
        CancellationToken cancellationToken = default)
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

internal sealed class SpyRunRecordWriter : IRunRecordWriter
{
    public List<RunRecord> Records { get; } = [];

    public Task<string> WriteAsync(RunRecord record, CancellationToken cancellationToken = default)
    {
        Records.Add(record);
        return Task.FromResult("chartula-runs/run.json");
    }
}

/// <summary>Plans for stub renderers: one empty plan per requested audience, in render order.</summary>
internal static class StubPlans
{
    public static IReadOnlyDictionary<Audience, RenderPlan> Empty(IReadOnlyCollection<Audience>? audiences)
        => new[] { Audience.Technical, Audience.Customer, Audience.Product }
            .Where(audience => audiences is null || audiences.Contains(audience))
            .ToDictionary(audience => audience, _ => new RenderPlan(new GroundedFacts([]), []));
}
