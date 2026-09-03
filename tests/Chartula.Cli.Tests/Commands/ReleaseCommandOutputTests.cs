using Chartula.Core.Llm;
using Chartula.Cli.Commands;
using Chartula.Core.Pipeline;
using Chartula.Core.PullRequests;

namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// What a run wrote - and what it deliberately did not - has to be readable from
/// its output, or a skipped publication looks like a complete run.
/// </summary>
public sealed class ReleaseCommandOutputTests
{
    private static async Task<string> RunAsync(
        PipelineMode mode, IReadOnlyList<string> written, IReadOnlyList<string> skipped)
    {
        ReleaseOutcome outcome = new(
            "v1.0.0",
            mode,
            [new AudienceOutcome(Audience.Technical, Success: true, "- Added search", [], Error: null)],
            written)
        {
            SkippedOutputs = skipped,
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
    public async Task A_generate_run_lists_what_it_wrote()
    {
        string text = await RunAsync(
            PipelineMode.Generate,
            ["changelog.json", "CHANGELOG.md", "https://github.com/octo/repo/releases/tag/v1.0.0"],
            []);

        Assert.Contains("Wrote:", text);
        Assert.Contains("  - https://github.com/octo/repo/releases/tag/v1.0.0", text);
        Assert.DoesNotContain("Skipped", text);
    }

    [Fact]
    public async Task A_run_without_publishing_names_the_release_it_left_alone()
    {
        string text = await RunAsync(
            PipelineMode.GenerateWithoutPublishing,
            ["changelog.json", "CHANGELOG.md"],
            ["Release notes for v1.0.0 in octo/repo"]);

        Assert.Contains("Wrote:", text);
        Assert.Contains("  - CHANGELOG.md", text);
        Assert.Contains("Skipped (--no-publish):", text);
        Assert.Contains("  - Release notes for v1.0.0 in octo/repo", text);
    }
}
