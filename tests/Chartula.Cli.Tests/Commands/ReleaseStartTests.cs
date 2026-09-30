using Chartula.Cli.Commands;
using Chartula.Core.Pipeline;
using Chartula.Core.PullRequests;

namespace Chartula.Cli.Tests.Commands;

public sealed class ReleaseStartTests
{
    [Fact]
    public void Without_a_named_start_the_release_starts_after_the_previous_tag()
    {
        Assert.True(ReleaseStart.TryParse(["preview"], out ReleaseStart start, out _));
        Assert.Equal(new ReleaseStart(Since: null), start);
    }

    [Fact]
    public void Reads_a_named_start()
    {
        Assert.True(ReleaseStart.TryParse(["preview", "--since", "v0.9.0"], out ReleaseStart start, out _));
        Assert.Equal(new ReleaseStart("v0.9.0"), start);
    }

    [Theory]
    [InlineData("preview", "--since")]
    [InlineData("preview", "--since", "--yes")]
    public void A_start_option_without_a_value_is_refused(params string[] args)
    {
        Assert.False(ReleaseStart.TryParse(args, out _, out string? error));
        Assert.Contains("--since needs", error);
    }

    [Theory]
    [InlineData("--since")]
    [InlineData("--yes")]
    public void The_help_names_the_option(string option) => Assert.Contains(option, Program.Usage);

    [Fact]
    public void The_help_no_longer_calls_a_range_the_whole_history()
        => Assert.DoesNotContain("whole", Program.Usage, StringComparison.OrdinalIgnoreCase);

    [Fact]
    public async Task A_range_nobody_confirmed_stops_without_being_called_an_error()
    {
        StringWriter output = new();

        int exitCode = await ReleaseCommand.RunAsync(
            new UnconfirmedPipeline(), PipelineMode.Preview,
            new ReleaseRequest("v0.1.0", new RepositoryCoordinates("octo", "repo")), output, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.StartsWith("Stopped: The range of v0.1.0 was not confirmed", output.ToString());
    }

    private sealed class UnconfirmedPipeline : IReleasePipeline
    {
        public Task<ReleaseOutcome> RunAsync(ReleaseRequest request, PipelineMode mode, CancellationToken cancellationToken = default)
            => throw new UnconfirmedRangeException(request.Tag);
    }
}
