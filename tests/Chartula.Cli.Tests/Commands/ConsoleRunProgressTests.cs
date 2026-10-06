using Chartula.Cli.Commands;
using Chartula.Cli.Terminal;
using Chartula.Core.Llm;
using Chartula.Core.Observability;

namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// #283: a run shows each step as it starts, with its count and the elapsed time. On a
/// live terminal the line updates in place with the Quiet Pulse spinner (#339); without
/// one each step is one plain line.
/// </summary>
public sealed class ConsoleRunProgressTests
{
    private static readonly ProgressStep Reading = new(RunStep.ReadingPullRequests);

    private static readonly TerminalProfile LiveWithoutColor = new(Live: true, ColorDepth.None, Unicode: true);

    [Fact]
    public void Without_a_live_terminal_each_step_is_one_plain_line_as_it_starts()
    {
        StringWriter output = new();
        ConsoleRunProgress progress = new(output, TerminalProfile.Plain);

        progress.Begin(Reading, 100);
        progress.Advance(37);
        progress.Begin(new ProgressStep(RunStep.Rendering, Audience.Technical));
        progress.Begin(new ProgressStep(RunStep.Checking, Audience.Technical));
        progress.Fail();

        Assert.Equal(
            "Reading pull requests (100 commits)\nRendering technical\nChecking technical\n",
            output.ToString().ReplaceLineEndings("\n"));
        Assert.DoesNotContain('\r', output.ToString().Replace(Environment.NewLine, "\n"));
        Assert.DoesNotContain('\u001b', output.ToString());
    }

    [Fact]
    public void On_a_live_terminal_the_line_shows_the_spinner_the_count_and_the_elapsed_time()
    {
        StringWriter output = new();
        ManualTime time = new();
        ConsoleRunProgress progress = new(output, LiveWithoutColor, time);

        progress.Begin(Reading, 100);
        time.Pass(TimeSpan.FromSeconds(4));
        progress.Advance(37);

        string last = output.ToString().Split("\r\u001b[K").Last();
        Assert.Equal("  ⠋      Reading pull requests   37/100 commits   4 s", last);
    }

    // A finished step keeps its line with the time it took, so the steps read as a list.
    [Fact]
    public void On_a_live_terminal_a_finished_step_stays_as_a_line_marked_done()
    {
        StringWriter output = new();
        ManualTime time = new();
        ConsoleRunProgress progress = new(output, LiveWithoutColor, time);

        progress.Begin(new ProgressStep(RunStep.Rendering, Audience.Technical));
        time.Pass(TimeSpan.FromSeconds(72));
        progress.Begin(new ProgressStep(RunStep.Rendering, Audience.Customer));
        progress.Complete();

        Assert.Equal(
            ["  · done Rendering technical                      1 min 12 s", "  · done Rendering customer                       0 s"],
            Lines(output));
    }

    // The step a run failed in is the last line above the error, and does not read as done.
    [Fact]
    public void The_step_a_run_failed_in_is_marked_failed()
    {
        StringWriter output = new();
        ManualTime time = new();
        ConsoleRunProgress progress = new(output, LiveWithoutColor, time);

        progress.Begin(Reading, 3);
        progress.Advance(1);
        progress.Fail();

        Assert.Equal(["  × fail Reading pull requests   1/3 commits      0 s"], Lines(output));
    }

    // A quick step shows only its result: the spinner would flash and be gone.
    [Fact]
    public void A_step_quicker_than_the_delay_shows_no_spinner()
    {
        StringWriter output = new();
        ManualTime time = new();
        ConsoleRunProgress progress = new(output, LiveWithoutColor, time);

        progress.Begin(Reading, 2);
        time.Pass(TimeSpan.FromMilliseconds(100));
        progress.Advance(2);
        progress.Complete();

        Assert.DoesNotContain("⠋", output.ToString());
        Assert.Equal(["  · done Reading pull requests   2/2 commits      0 s"], Lines(output));
    }

    [Fact]
    public void Without_unicode_the_spinner_and_the_marks_are_ascii()
    {
        StringWriter output = new();
        ManualTime time = new();
        ConsoleRunProgress progress = new(output, new TerminalProfile(Live: true, ColorDepth.None, Unicode: false), time);

        progress.Begin(Reading, 3);
        time.Pass(TimeSpan.FromSeconds(1));
        progress.Advance(1);
        progress.Fail();

        Assert.All(output.ToString(), character => Assert.True(character < 128));
        Assert.Equal(["  x fail Reading pull requests   1/3 commits      1 s"], Lines(output));
    }

    [Fact]
    public void Completing_twice_or_without_a_step_writes_nothing_more()
    {
        StringWriter output = new();
        ConsoleRunProgress progress = new(output, LiveWithoutColor, new ManualTime());

        progress.Complete();
        progress.Complete();

        Assert.Empty(output.ToString());
    }

    // What a reader sees: each line as last drawn.
    private static string[] Lines(StringWriter output)
        => [.. output.ToString().Replace(Environment.NewLine, "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Split("\r\u001b[K").Last())];

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
