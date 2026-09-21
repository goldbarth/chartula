using Chartula.Cli.Commands;
using Chartula.Core.Pipeline;
using Chartula.Core.PullRequests;

namespace Chartula.Cli.Tests.Commands;

public sealed class ReleaseStartTests
{
    [Fact]
    public void Neither_option_leaves_the_start_to_the_previous_tag()
    {
        Assert.True(ReleaseStart.TryParse(["preview"], out ReleaseStart start, out _));
        Assert.Equal(new ReleaseStart(null, false), start);
    }

    [Fact]
    public void Reads_a_named_start()
    {
        Assert.True(ReleaseStart.TryParse(["preview", "--since", "v0.9.0"], out ReleaseStart start, out _));
        Assert.Equal(new ReleaseStart("v0.9.0", false), start);
    }

    [Fact]
    public void Reads_the_whole_history_flag()
    {
        Assert.True(ReleaseStart.TryParse(["preview", "--whole-history"], out ReleaseStart start, out _));
        Assert.Equal(new ReleaseStart(null, true), start);
    }

    [Theory]
    [InlineData("preview", "--since")]
    [InlineData("preview", "--since", "--whole-history")]
    public void A_start_option_without_a_value_is_refused(params string[] args)
    {
        Assert.False(ReleaseStart.TryParse(args, out _, out string? error));
        Assert.Contains("--since needs", error);
    }

    [Fact]
    public void Both_are_refused_because_they_answer_the_same_question()
    {
        Assert.False(ReleaseStart.TryParse(["preview", "--since", "v0.9.0", "--whole-history"], out _, out string? error));
        Assert.Contains("not both", error);
    }

    [Theory]
    [InlineData("--since")]
    [InlineData("--whole-history")]
    public void The_help_names_both(string option) => Assert.Contains(option, Program.Usage);

    [Fact]
    public async Task A_refused_first_tag_names_the_cause_and_both_ways_out()
    {
        StringWriter output = new();

        int exitCode = await ReleaseCommand.RunAsync(
            new RefusingPipeline(), PipelineMode.Preview,
            new ReleaseRequest("v0.1.0", new RepositoryCoordinates("octo", "repo")), output, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Contains("v0.1.0 is the first tag", output.ToString());
        Assert.Contains("(69 commits)", output.ToString());
        Assert.Contains("--since <ref>", output.ToString());
        Assert.Contains("--whole-history", output.ToString());
    }

    private sealed class RefusingPipeline : IReleasePipeline
    {
        public Task<ReleaseOutcome> RunAsync(ReleaseRequest request, PipelineMode mode, CancellationToken cancellationToken = default)
            => throw new WholeHistoryException(request.Tag, 69);
    }
}
