using Chartula.Cli.Commands;
using Chartula.Core.Categorization;
using Chartula.Core.Curation;
using Chartula.Core.Facts;
using Chartula.Core.Llm;
using Chartula.Core.Observability;
using Chartula.Core.Pipeline;
using Chartula.Core.PullRequests;

namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// #259: a preview makes no model call. It shows what generate would render from - each
/// fact with the audiences it reaches, what was dropped and why - and what generate
/// would send.
/// </summary>
public sealed class PreviewOutputTests
{
    private static readonly ChangeFact Search = new(
        "feat: add search", 1, "https://example/pull/1", ChangeCategory.Feature,
        IsUserVisible: true, IsBreaking: false, [], [], "Adds search.");

    private static readonly ChangeFact Guide = new(
        "docs: rewrite the guide", 4, "https://example/pull/4", ChangeCategory.Documentation,
        IsUserVisible: false, IsBreaking: true, [], [], Description: null);

    private static readonly DroppedChange Chore = new(
        new ReleaseChange("chore: bump deps", null, null, null, [], ChangeSource.Commit, "0123456789abcdef"),
        "Internal is in filter.excludeCategories");

    private static async Task<(int ExitCode, string Output)> RunAsync(ReleasePreview preview)
    {
        RunMetrics metrics = new();
        metrics.RecordReleaseScope(new ReleaseScope(3, 2, 2, 1, 12));
        ReleaseOutcome outcome = new("v1.0.0", PipelineMode.Preview, [], [], metrics.Snapshot()) { Preview = preview };

        StringWriter output = new();
        int exitCode = await ReleaseCommand.RunAsync(
            new StubPipeline(outcome),
            PipelineMode.Preview,
            new ReleaseRequest("v1.0.0", new RepositoryCoordinates("octo", "repo")),
            output,
            CancellationToken.None);
        return (exitCode, output.ToString().ReplaceLineEndings("\n"));
    }

    private static ReleasePreview Preview(bool thoroughCheck = true) => new(
        [new PreviewFact(Search, [Audience.Technical, Audience.Customer]), new PreviewFact(Guide, [])],
        [Chore],
        [new AudiencePreview(Audience.Technical, 1, 21_310), new AudiencePreview(Audience.Customer, 0, 0)],
        thoroughCheck);

    [Fact]
    public async Task A_preview_shows_each_fact_with_its_audiences_and_what_was_dropped_and_why()
    {
        (int exitCode, string text) = await RunAsync(Preview());

        Assert.Equal(0, exitCode);
        Assert.StartsWith("Preview of v1.0.0\nNo model calls, no files written, no publication\n\n", text);
        Assert.Contains("Release: 3 commits, 2 pull requests, 2 facts (1 with a description, 12 characters)\n", text);
        Assert.Contains("Facts (2):\n  #1       Feature: feat: add search\n           technical, customer\n", text);
        Assert.Contains("  #4       Documentation, breaking: docs: rewrite the guide\n           in no rendering\n", text);
        Assert.Contains("Dropped (1):\n  0123456  chore: bump deps\n           Internal is in filter.excludeCategories\n", text);
    }

    [Fact]
    public async Task A_preview_says_what_generate_would_send_in_calls_and_characters()
    {
        (_, string text) = await RunAsync(Preview());

        Assert.Contains("generate would make 2 model calls:\n", text);
        Assert.Contains("  technical 1 rephrasing call, 21,310 characters of prompt, then 1 thorough check\n", text);
        Assert.Contains("  customer  nothing to render, no call\n", text);
        Assert.DoesNotContain("tokens", text);
        Assert.DoesNotContain("Run metrics", text);
    }

    [Fact]
    public async Task Without_the_thorough_check_a_preview_counts_the_rephrasing_only()
    {
        (_, string text) = await RunAsync(Preview(thoroughCheck: false));

        Assert.Contains("generate would make 1 model call:\n", text);
        Assert.DoesNotContain("thorough check", text);
    }
}
