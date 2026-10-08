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

    // A width that cannot be read: the line is written as if nothing were known. The tests
    // on the line's form pass it, so they do not depend on the console of the test runner.
    private static readonly Func<int?> NoWidth = static () => null;

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
        ConsoleRunProgress progress = new(output, LiveWithoutColor, time, NoWidth);

        progress.Begin(Reading, 100);
        time.Pass(TimeSpan.FromSeconds(4));
        progress.Advance(37);

        string last = output.ToString().Split("\r\u001b[K").Last();
        Assert.Equal("       ⠋ Reading pull requests   37/100 commits   4 s", last);
    }

    // A finished step keeps its line with the time it took, so the steps read as a list.
    [Fact]
    public void On_a_live_terminal_a_finished_step_stays_as_a_line_marked_done()
    {
        StringWriter output = new();
        ManualTime time = new();
        ConsoleRunProgress progress = new(output, LiveWithoutColor, time, NoWidth);

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
        ConsoleRunProgress progress = new(output, LiveWithoutColor, time, NoWidth);

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
        ConsoleRunProgress progress = new(output, LiveWithoutColor, time, NoWidth);

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
        ConsoleRunProgress progress = new(output, new TerminalProfile(Live: true, ColorDepth.None, Unicode: false), time, NoWidth);

        progress.Begin(Reading, 3);
        time.Pass(TimeSpan.FromSeconds(1));
        progress.Advance(1);
        progress.Fail();

        Assert.All(output.ToString(), character => Assert.True(character < 128));
        Assert.Equal(["  x fail Reading pull requests   1/3 commits      1 s"], Lines(output));
    }

    // #351: the glyph is read with the step it belongs to. One space, because an ASCII
    // frame that touches the label would read "|Reading".
    [Fact]
    public void Without_unicode_the_spinner_stands_one_space_before_its_label_too()
    {
        StringWriter output = new();
        ManualTime time = new();
        ConsoleRunProgress progress = new(output, new TerminalProfile(Live: true, ColorDepth.None, Unicode: false), time, NoWidth);

        progress.Begin(Reading, 3);
        time.Pass(TimeSpan.FromSeconds(1));
        progress.Advance(1);

        string last = output.ToString().Split("\r\u001b[K").Last();
        Assert.Equal("       - Reading pull requests   1/3 commits      1 s", last);
    }

    // #351: the mark fills the columns the spinner stood at the end of, so the label, the
    // count and the time of a step do not move when it ends.
    [Theory]
    [InlineData(false, "  · done ")]
    [InlineData(true, "  × fail ")]
    public void Only_the_status_columns_change_when_a_step_ends(bool failed, string mark)
    {
        StringWriter output = new();
        ManualTime time = new();
        ConsoleRunProgress progress = new(output, LiveWithoutColor, time, NoWidth);

        progress.Begin(Reading, 3);
        time.Pass(TimeSpan.FromSeconds(4));
        progress.Advance(1);
        string running = output.ToString().Split("\r\u001b[K").Last();
        if (failed)
        {
            progress.Fail();
        }
        else
        {
            progress.Complete();
        }

        string ended = Lines(output).Single();
        Assert.Equal("       ⠋ ", running[..mark.Length]);
        Assert.Equal(mark, ended[..mark.Length]);
        Assert.Equal(running[mark.Length..], ended[mark.Length..]);
    }

    // #349: a line wider than the terminal wraps, the carriage return goes back to the start
    // of its last row only, and every frame leaves a row behind. So the line gives up whole
    // parts until it fits: the padding and the word "commits", the time, the count.
    [Theory]
    [InlineData(80, "       ⠋ Reading pull requests   37/100 commits   4 s")]
    [InlineData(54, "       ⠋ Reading pull requests   37/100 commits   4 s")]
    [InlineData(53, "       ⠋ Reading pull requests 37/100 4 s")]
    [InlineData(42, "       ⠋ Reading pull requests 37/100 4 s")]
    [InlineData(41, "       ⠋ Reading pull requests 37/100")]
    [InlineData(38, "       ⠋ Reading pull requests 37/100")]
    [InlineData(37, "       ⠋ Reading pull requests")]
    [InlineData(31, "       ⠋ Reading pull requests")]
    [InlineData(30, "       ⠋ Reading pull request")]
    [InlineData(10, "       ⠋ ")]
    public void A_running_step_is_fitted_to_the_width_of_the_terminal(int columns, string expected)
    {
        StringWriter output = new();
        ManualTime time = new();
        ConsoleRunProgress progress = new(output, LiveWithoutColor, time, () => columns);

        progress.Begin(Reading, 100);
        time.Pass(TimeSpan.FromSeconds(4));
        progress.Advance(37);

        Assert.Equal(expected, output.ToString().Split("\r\u001b[K").Last());
    }

    // A step without a count keeps its time for as long as there is room for it.
    [Theory]
    [InlineData(48, "  · done Rendering technical 1 min 12 s")]
    [InlineData(39, "  · done Rendering technical")]
    public void A_finished_step_is_fitted_to_the_same_width(int columns, string expected)
    {
        StringWriter output = new();
        ManualTime time = new();
        ConsoleRunProgress progress = new(output, LiveWithoutColor, time, () => columns);

        progress.Begin(new ProgressStep(RunStep.Rendering, Audience.Technical));
        time.Pass(TimeSpan.FromSeconds(72));
        progress.Complete();

        Assert.Equal([expected], Lines(output));
    }

    // What the issue asks for, at every width a line can be drawn in: no frame and no
    // finished line reaches the last column, so none wraps and none is left behind.
    [Fact]
    public void No_line_is_wider_than_the_terminal_at_any_width()
    {
        for (int columns = 10; columns <= 70; columns++)
        {
            StringWriter output = new();
            ManualTime time = new();
            int width = columns;
            ConsoleRunProgress progress = new(output, LiveWithoutColor, time, () => width);

            progress.Begin(Reading, 1_000);
            time.Pass(TimeSpan.FromSeconds(125));
            progress.Advance(999);
            progress.Begin(new ProgressStep(RunStep.Rendering, Audience.Technical));
            time.Pass(TimeSpan.FromSeconds(125));
            progress.Fail();

            string[] drawn = output.ToString().Replace(Environment.NewLine, "\n")
                .Split(["\r\u001b[K", "\n"], StringSplitOptions.RemoveEmptyEntries);
            Assert.NotEmpty(drawn);
            Assert.All(drawn, line => Assert.True(line.Length < width, $"{width} columns: '{line}'"));
        }
    }

    // Narrower than the spinner and its indent there is nothing to fit: the step shows no
    // frames, and its finished line once.
    [Fact]
    public void Below_ten_columns_a_step_draws_no_frames()
    {
        StringWriter output = new();
        ManualTime time = new();
        ConsoleRunProgress progress = new(output, LiveWithoutColor, time, static () => 9);

        progress.Begin(Reading, 3);
        time.Pass(TimeSpan.FromSeconds(1));
        progress.Advance(1);
        progress.Advance(2);

        Assert.Empty(output.ToString());

        progress.Complete();

        Assert.Equal(["  · done "], Lines(output));
    }

    // The width is asked for with every line, so a window resized during a step is right
    // from the next frame on.
    [Fact]
    public void A_resize_during_a_step_shows_in_the_next_frame()
    {
        StringWriter output = new();
        ManualTime time = new();
        int columns = 80;
        ConsoleRunProgress progress = new(output, LiveWithoutColor, time, () => columns);

        progress.Begin(Reading, 100);
        time.Pass(TimeSpan.FromSeconds(4));
        progress.Advance(37);
        columns = 40;
        progress.Advance(38);

        Assert.Equal(
            ["       ⠋ Reading pull requests   37/100 commits   4 s", "       ⠋ Reading pull requests 38/100"],
            output.ToString().Split("\r\u001b[K", StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void Completing_twice_or_without_a_step_writes_nothing_more()
    {
        StringWriter output = new();
        ConsoleRunProgress progress = new(output, LiveWithoutColor, new ManualTime(), NoWidth);

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
