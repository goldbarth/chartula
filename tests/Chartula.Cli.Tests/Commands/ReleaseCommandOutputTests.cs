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

    [Fact]
    public async Task A_rendering_with_a_description_shows_it_above_the_text()
    {
        // The description opens the published page, so a preview that hides it
        // vouches for everything but the first line the reader will see.
        ReleaseOutcome outcome = new(
            "v1.0.0",
            PipelineMode.Preview,
            [
                new AudienceOutcome(Audience.Customer, Success: true, "- Search is here.", [], Error: null)
                {
                    Description = "A release about finding things.",
                },
            ],
            []);

        StringWriter output = new();
        await ReleaseCommand.RunAsync(
            new StubPipeline(outcome),
            PipelineMode.Preview,
            new ReleaseRequest("v1.0.0", new RepositoryCoordinates("octo", "repo")),
            output,
            CancellationToken.None);

        string text = output.ToString();
        Assert.Contains("  description: A release about finding things.", text, StringComparison.Ordinal);
        Assert.True(
            text.IndexOf("description:", StringComparison.Ordinal)
            < text.IndexOf("- Search is here.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_rendering_without_a_description_shows_no_empty_line_for_one()
    {
        string text = await RunAsync(PipelineMode.Generate, ["changelog.json"], []);

        Assert.DoesNotContain("description:", text, StringComparison.Ordinal);
    }
}
