using Chartula.Cli.Commands;
using Chartula.Core.Llm;
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

    private static async Task<(int ExitCode, string Output)> RunAsync(PipelineMode mode, params bool[] rendered)
    {
        Audience[] audiences = [Audience.Technical, Audience.Customer, Audience.Product];
        ReleaseOutcome outcome = new(
            "v1.0.0",
            mode,
            [.. rendered.Select((success, i) => success
                ? new AudienceOutcome(audiences[i], Success: true, "- Added search", [], Error: null)
                : new AudienceOutcome(audiences[i], Success: false, Text: null, [], Error: "Status Code: Unauthorized"))],
            rendered.Any(success => success) && mode != PipelineMode.Preview ? ["changelog.json"] : []);

        StringWriter output = new();
        int exitCode = await ReleaseCommand.RunAsync(
            new StubPipeline(outcome),
            mode,
            new ReleaseRequest("v1.0.0", new RepositoryCoordinates("octo", "repo")),
            output,
            CancellationToken.None);

        return (exitCode, output.ToString());
    }

    [Theory]
    [InlineData(PipelineMode.Preview)]
    [InlineData(PipelineMode.Generate)]
    public async Task A_run_in_which_every_audience_rendered_exits_zero(PipelineMode mode)
    {
        (int exitCode, _) = await RunAsync(mode, true, true, true);

        Assert.Equal(0, exitCode);
    }

    [Theory]
    [InlineData(PipelineMode.Preview)]
    [InlineData(PipelineMode.GenerateWithoutPublishing)]
    public async Task A_run_in_which_one_audience_failed_exits_non_zero_and_counts_it(PipelineMode mode)
    {
        (int exitCode, string text) = await RunAsync(mode, false, true, true);

        Assert.Equal(1, exitCode);
        Assert.Contains("1 of 3 audiences failed.", text);
    }

    [Fact]
    public async Task A_generate_run_in_which_no_audience_rendered_does_not_claim_it_generated_anything()
    {
        (int exitCode, string text) = await RunAsync(PipelineMode.Generate, false, false, false);

        Assert.Equal(1, exitCode);
        Assert.StartsWith("No changelog generated for v1.0.0: no audience rendered.", text);
        Assert.DoesNotContain("Generated changelog", text);
        Assert.Contains("Nothing to write.", text);
    }

    [Fact]
    public async Task A_preview_in_which_no_audience_rendered_says_so()
    {
        (int exitCode, string text) = await RunAsync(PipelineMode.Preview, false, false, false);

        Assert.Equal(1, exitCode);
        Assert.StartsWith("No preview for v1.0.0: no audience rendered.", text);
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

    // #219: a refused publication is listed after what was written, fails the exit
    // code, and says what a second run would cost.
    [Fact]
    public async Task A_refused_publication_lists_the_written_files_and_fails_the_run()
    {
        ReleaseOutcome outcome = new(
            "v1.0.0",
            PipelineMode.Generate,
            [new AudienceOutcome(Audience.Technical, Success: true, "- Added search", [], Error: null)],
            ["changelog.json", "CHANGELOG.md"])
        {
            PublishFailure = "GitHub refused to publish the release notes for v1.0.0 to octo/repo (403 Forbidden).\n  Publishing needs a token with Contents read and write on octo/repo.",
        };

        StringWriter output = new();
        int exitCode = await ReleaseCommand.RunAsync(
            new StubPipeline(outcome),
            PipelineMode.Generate,
            new ReleaseRequest("v1.0.0", new RepositoryCoordinates("octo", "repo")),
            output,
            CancellationToken.None);
        string text = output.ToString();

        Assert.Equal(1, exitCode);
        Assert.Contains("Wrote:\n  - changelog.json\n  - CHANGELOG.md\nNot published: the release notes.\n", text.ReplaceLineEndings("\n"));
        Assert.Contains("  GitHub refused to publish the release notes for v1.0.0 to octo/repo (403 Forbidden).", text);
        Assert.Contains("    Publishing needs a token with Contents read and write on octo/repo.", text);
        Assert.Contains("--no-publish skips this step", text);
        Assert.DoesNotContain("Error:", text);
    }

    private static async Task<string> FormatAsync(params AudienceOutcome[] renderings)
    {
        StringWriter output = new();
        await ReleaseCommand.RunAsync(
            new StubPipeline(new ReleaseOutcome("v1.0.0", PipelineMode.Preview, renderings, [])),
            PipelineMode.Preview,
            new ReleaseRequest("v1.0.0", new RepositoryCoordinates("octo", "repo")),
            output,
            CancellationToken.None);
        return output.ToString().ReplaceLineEndings("\n");
    }

    private static AudienceOutcome Failed(Audience audience, string error)
        => new(audience, Success: false, Text: null, [], error);

    // #234: one endpoint refusing one model is one problem, however many audiences asked.
    [Fact]
    public async Task Audiences_failing_for_the_same_reason_say_it_once()
    {
        const string Error = "Changelog generation for 'v1.0.0' failed: anthropic at https://x/v1/messages answered 404 Not Found for model 'm'.";

        string text = await FormatAsync(
            Failed(Audience.Technical, Error), Failed(Audience.Customer, Error), Failed(Audience.Product, Error));

        Assert.Equal(2, text.Split("answered 404").Length);
        Assert.Contains("--- Customer ---\n  (failed) The same as Technical.\n", text);
        Assert.Contains("--- Product ---\n  (failed) The same as Technical.\n", text);
    }

    [Fact]
    public async Task Audiences_failing_for_different_reasons_say_each()
    {
        string text = await FormatAsync(
            Failed(Audience.Technical, "the model's answer did not match the expected entry format"),
            Failed(Audience.Customer, "anthropic at https://x/v1/messages answered 529 Overloaded for model 'm'."));

        Assert.Contains("  (failed) the model's answer did not match the expected entry format\n", text);
        Assert.Contains("  (failed) anthropic at https://x/v1/messages answered 529 Overloaded for model 'm'.\n", text);
        Assert.DoesNotContain("The same as", text);
    }

    // A failed call explains itself over several lines; flush-left they would read as
    // output of their own.
    [Fact]
    public async Task A_failure_over_several_lines_stays_indented_under_its_audience()
    {
        string text = await FormatAsync(
            Failed(Audience.Technical, "answered 401 Unauthorized.\nThe endpoint rejected the key in ANTHROPIC_API_KEY."),
            new AudienceOutcome(
                Audience.Customer, Success: true, "- Added search", ["The thorough check could not be evaluated: answered 404.\nThe endpoint said: no."], Error: null));

        Assert.Contains("  (failed) answered 401 Unauthorized.\n           The endpoint rejected the key in ANTHROPIC_API_KEY.\n", text);
        Assert.Contains("    ! The thorough check could not be evaluated: answered 404.\n      The endpoint said: no.\n", text);
    }
}
