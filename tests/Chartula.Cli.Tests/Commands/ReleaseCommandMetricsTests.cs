using Chartula.Cli.Commands;
using Chartula.Core.Llm;
using Chartula.Core.Observability;
using Chartula.Core.Pipeline;
using Chartula.Core.PullRequests;

namespace Chartula.Cli.Tests.Commands;

/// <summary>A pipeline that returns a fixed outcome, so the command's output can be read.</summary>
internal sealed class StubPipeline(ReleaseOutcome outcome) : IReleasePipeline
{
    public Task<ReleaseOutcome> RunAsync(
        ReleaseRequest request, PipelineMode mode, CancellationToken cancellationToken = default)
        => Task.FromResult(outcome);
}

/// <summary>Every run prints what it did and what it cost, so the numbers are there to read.</summary>
public sealed class ReleaseCommandMetricsTests
{
    private static async Task<string> RunAsync(PipelineMode mode, string? runRecord = null)
    {
        RunMetrics metrics = new();
        metrics.RecordFaithfulnessChecks(["shared"], ["shared", "only thorough"], thoroughEvaluated: true);
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(1_500, 300));
        metrics.RecordLlmCall(LlmOperation.FaithfulnessCheck, new LlmCall(2_000, 40));

        ReleaseOutcome outcome = new(
            "v1.0.0",
            mode,
            [new AudienceOutcome(Audience.Technical, Success: true, "- Added search", [], Error: null)],
            [],
            metrics.Snapshot())
        {
            RunRecord = runRecord,
        };

        StringWriter output = new();
        await ReleaseCommand.RunAsync(
            new StubPipeline(outcome),
            mode,
            new ReleaseRequest("v1.0.0", new RepositoryCoordinates("octo", "repo")),
            output,
            CancellationToken.None);

        return output.ToString();
    }

    [Fact]
    public async Task A_generate_run_prints_its_token_usage_and_check_activity()
    {
        string text = await RunAsync(PipelineMode.Generate);

        Assert.Contains("Run metrics", text);
        Assert.Contains("3,840 tokens", text);
        Assert.Contains("caught 1 claim the rule-based check missed, for 2,040 tokens in 1 call", text);
    }

    [Fact]
    public async Task A_preview_run_reports_its_cost_too()
    {
        string text = await RunAsync(PipelineMode.Preview);

        // A preview writes nothing, but it still spends tokens.
        Assert.Contains("Preview only - nothing was written or published.", text);
        Assert.Contains("3,840 tokens", text);
    }

    [Fact]
    public async Task A_run_that_kept_a_record_says_where_under_its_metrics()
    {
        string text = await RunAsync(PipelineMode.Generate, "chartula-runs/20260922T123015Z-v1.0.0.json");

        Assert.Contains("  Total:            3,840 tokens", text);
        Assert.EndsWith("  Recorded in chartula-runs/20260922T123015Z-v1.0.0.json" + Environment.NewLine, text);
    }

    [Fact]
    public async Task A_run_without_a_record_names_none()
    {
        string text = await RunAsync(PipelineMode.Preview);

        Assert.DoesNotContain("Recorded in", text);
    }
}
