using System.Globalization;
using Chartula.Cli.Terminal;
using Chartula.Core.Observability;

namespace Chartula.Cli.Commands;

/// <summary>
/// Shows a run's steps on stderr, so a changelog redirected from stdout stays clean (#283).
/// On a live terminal the current step is one line with the Quiet Pulse spinner, its count
/// and its elapsed time, redrawn in place (#339). A step that ends stays as a line of its
/// own, marked <c>· done</c> or, for the step a run failed in, <c>× fail</c>; "done" says the
/// step ended, not that its result is good.
/// Without a live terminal, as in CI or with <c>--plain</c>, each step is one plain line as
/// it starts, with no control characters, so a job log stays readable and shows the step a
/// run was in when it stopped.
/// </summary>
/// <param name="output">Where the steps go: stderr.</param>
/// <param name="profile">What <paramref name="output"/> may show.</param>
/// <param name="time">The clock; the system clock unless a test sets one.</param>
internal sealed class ConsoleRunProgress(TextWriter output, TerminalProfile profile, TimeProvider? time = null)
    : IRunProgress, IDisposable
{
    // Wide enough for "Reading pull requests" and "Rendering technical".
    private const int LabelWidth = 24;
    private const int CountWidth = 17;

    // Carriage return, then clear to the end of the line: the line is rewritten in place.
    private const string Rewrite = "\r\u001b[K";

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Lock _lock = new();

    // One clock for every spinner of the run, so the fade continues from step to step.
    private readonly long _created = (time ?? TimeProvider.System).GetTimestamp();
    private ITimer? _ticker;
    private string? _label;
    private int? _total;
    private int _done;
    private long _started;
    private bool _shown;

    public void Begin(ProgressStep step, int? total = null)
    {
        lock (_lock)
        {
            EndStep(failed: false);
            _label = Label(step);
            _total = total;
            _done = 0;
            _started = _time.GetTimestamp();
            _shown = false;

            if (!profile.Live)
            {
                output.WriteLine(total is { } count ? $"{_label} ({Commits(count)})" : _label);
                output.Flush();
                return;
            }

            // A step shorter than the delay never shows a spinner, only its result. The
            // ticker also keeps the elapsed time moving while a model call runs.
            _ticker ??= _time.CreateTimer(_ => Tick(), null, QuietPulse.FrameTime, QuietPulse.FrameTime);
        }
    }

    public void Advance(int done)
    {
        lock (_lock)
        {
            _done = done;
            if (profile.Live && _label is not null && Due())
            {
                Draw();
            }
        }
    }

    public void Complete() => Stop(failed: false);

    public void Fail() => Stop(failed: true);

    public void Dispose() => Complete();

    private void Stop(bool failed)
    {
        lock (_lock)
        {
            EndStep(failed);
            _ticker?.Dispose();
            _ticker = null;
        }
    }

    private void Tick()
    {
        lock (_lock)
        {
            if (_label is not null && Due())
            {
                Draw();
            }
        }
    }

    private bool Due() => _shown || _time.GetElapsedTime(_started) >= QuietPulse.Delay;

    // A finished step keeps its line, with the time it took.
    private void EndStep(bool failed)
    {
        if (profile.Live && _label is not null)
        {
            string status = StatusMarks.Format(failed ? StatusMark.Fail : StatusMark.Done, profile);
            output.Write($"{Rewrite}{Line(status)}");
            output.WriteLine();
            output.Flush();
        }

        _label = null;
    }

    private void Draw()
    {
        _shown = true;
        string glyph = QuietPulse.Glyph(_time.GetElapsedTime(_created), profile);
        output.Write($"{Rewrite}{Line(glyph + "     ")}");
        output.Flush();
    }

    // The status takes six columns, a symbol and its word, where the spinner stands
    // while the step runs, so the labels line up whatever the state.
    private string Line(string status)
    {
        string count = _total is { } total ? $"{_done}/{Commits(total)}" : string.Empty;
        string elapsed = Elapsed(_time.GetElapsedTime(_started));
        return $"  {status} {_label!.PadRight(LabelWidth)}{count.PadRight(CountWidth)}{elapsed}";
    }

    private static string Label(ProgressStep step)
    {
        string audience = step.Audience?.ToString().ToLowerInvariant() ?? string.Empty;
        return step.Kind switch
        {
            RunStep.ReadingPullRequests => "Reading pull requests",
            RunStep.Rendering => $"Rendering {audience}",
            RunStep.Checking => $"Checking {audience}",
            _ => step.Kind.ToString(),
        };
    }

    private static string Commits(int count)
        => count == 1 ? "1 commit" : $"{count.ToString("N0", CultureInfo.InvariantCulture)} commits";

    private static string Elapsed(TimeSpan elapsed)
        => elapsed.TotalSeconds < 60
            ? $"{(int)elapsed.TotalSeconds} s"
            : $"{(int)elapsed.TotalMinutes} min {elapsed.Seconds:00} s";
}
