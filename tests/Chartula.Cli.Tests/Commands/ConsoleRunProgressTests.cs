using Chartula.Cli.Commands;
using Chartula.Core.Llm;
using Chartula.Core.Observability;

namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// #283: a run shows each step as it starts, with its count and the elapsed time. In a
/// terminal the line updates in place; without one each step is one plain line.
/// </summary>
public sealed class ConsoleRunProgressTests
{
    private static readonly ProgressStep Reading = new(RunStep.ReadingPullRequests);

    [Fact]
    public void Without_a_terminal_each_step_is_one_plain_line_as_it_starts()
    {
        StringWriter output = new();
        ConsoleRunProgress progress = new(output, interactive: false);

        progress.Begin(Reading, 100);
        progress.Advance(37);
        progress.Begin(new ProgressStep(RunStep.Rendering, Audience.Technical));
        progress.Begin(new ProgressStep(RunStep.Checking, Audience.Technical));
        progress.Complete();

        Assert.Equal(
            "Reading pull requests (100 commits)\nRendering technical\nChecking technical\n",
            output.ToString().ReplaceLineEndings("\n"));
        Assert.DoesNotContain('\r', output.ToString().Replace(Environment.NewLine, "\n"));
        Assert.DoesNotContain('\u001b', output.ToString());
    }

    [Fact]
    public void In_a_terminal_the_line_updates_in_place_with_the_count_and_the_elapsed_time()
    {
        StringWriter output = new();
        ManualTime time = new();
        ConsoleRunProgress progress = new(output, interactive: true, time);

        progress.Begin(Reading, 100);
        time.Pass(TimeSpan.FromSeconds(4));
        progress.Advance(37);

        string last = output.ToString().Split("\r\u001b[K").Last();
        Assert.Equal("Reading pull requests   37/100 commits   4 s", last);
    }

    // A finished step keeps its line with the time it took, so the steps read as a list.
    [Fact]
    public void In_a_terminal_a_finished_step_stays_as_a_line_of_its_own()
    {
        StringWriter output = new();
        ManualTime time = new();
        ConsoleRunProgress progress = new(output, interactive: true, time);

        progress.Begin(new ProgressStep(RunStep.Rendering, Audience.Technical));
        time.Pass(TimeSpan.FromSeconds(72));
        progress.Begin(new ProgressStep(RunStep.Rendering, Audience.Customer));
        progress.Complete();

        string[] shown = [.. output.ToString().Replace(Environment.NewLine, "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Split("\r\u001b[K").Last())];
        Assert.Equal(["Rendering technical                      1 min 12 s", "Rendering customer                       0 s"], shown);
    }

    [Fact]
    public void Completing_twice_or_without_a_step_writes_nothing_more()
    {
        StringWriter output = new();
        ConsoleRunProgress progress = new(output, interactive: true, new ManualTime());

        progress.Complete();
        progress.Complete();

        Assert.Empty(output.ToString());
    }

    /// <summary>A clock a test moves by hand; its timers never fire on their own.</summary>
    private sealed class ManualTime : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Pass(TimeSpan span) => _ticks += span.Ticks;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            => new NeverFires();

        private sealed class NeverFires : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
