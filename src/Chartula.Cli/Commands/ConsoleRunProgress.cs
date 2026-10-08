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
/// <para>
/// A line redrawn in place has to fit in one row, so it is fitted to the terminal's width
/// each time it is written (#349): first without the padding and the word "commits", then
/// without the time, then without the count, and at last with the label cut.
/// </para>
/// </summary>
/// <param name="output">Where the steps go: stderr.</param>
/// <param name="profile">What <paramref name="output"/> may show.</param>
/// <param name="time">The clock; the system clock unless a test sets one.</param>
/// <param name="width">The terminal's columns, asked for each line; the console's unless a test sets it.</param>
internal sealed class ConsoleRunProgress(
    TextWriter output, TerminalProfile profile, TimeProvider? time = null, Func<int?>? width = null)
    : IRunProgress, IDisposable
{
    // Wide enough for "Reading pull requests" and "Rendering technical".
    private const int LabelWidth = 24;
    private const int CountWidth = 17;

    // The indent, the six status columns and the space before the label.
    private const int StatusColumns = 9;

    // Carriage return, then clear to the end of the line: the line is rewritten in place.
    private const string Rewrite = "\r\u001b[K";

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Func<int?> _width = width ?? TerminalWidth.Current;
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
        // Narrower than the spinner and its indent, every frame would wrap and leave a
        // row behind. The step then shows its finished line only.
        if (_width() is { } columns && TerminalWidth.Room(columns) < StatusColumns)
        {
            return;
        }

        _shown = true;
        string glyph = QuietPulse.Glyph(_time.GetElapsedTime(_created), profile);
        output.Write($"{Rewrite}{Line("     " + glyph)}");
        output.Flush();
    }

    // The status takes six columns, a symbol and its word. While the step runs the
    // spinner stands in the last of them, next to the label it animates (#351), so the
    // labels line up whatever the state and nothing moves when the step ends.
    private string Line(string status)
    {
        string count = _total is { } total ? $"{_done}/{Commits(total)}" : string.Empty;
        string elapsed = Elapsed(_time.GetElapsedTime(_started));
        string text = $"{_label!.PadRight(LabelWidth)}{count.PadRight(CountWidth)}{elapsed}";
        if (_width() is { } columns)
        {
            text = Fit(text, elapsed, Math.Max(0, TerminalWidth.Room(columns) - StatusColumns));
        }

        return $"  {status} {text}";
    }

    // The widest form that fits. Whole parts go before a part is cut, so a narrow line
    // never ends in half a number: the count says more than the time where a step has
    // one, and the label is what says which step this is.
    private string Fit(string full, string elapsed, int room)
    {
        if (full.Length <= room)
        {
            return full;
        }

        string label = _label!;
        string[] narrower = _total is { } total
            ? [$"{label} {_done}/{Number(total)} {elapsed}", $"{label} {_done}/{Number(total)}", label]
            : [$"{label} {elapsed}", label];
        return narrower.FirstOrDefault(text => text.Length <= room) ?? label[..room];
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

    private static string Commits(int count) => count == 1 ? "1 commit" : $"{Number(count)} commits";

    private static string Number(int count) => count.ToString("N0", CultureInfo.InvariantCulture);

    private static string Elapsed(TimeSpan elapsed)
        => elapsed.TotalSeconds < 60
            ? $"{(int)elapsed.TotalSeconds} s"
            : $"{(int)elapsed.TotalMinutes} min {elapsed.Seconds:00} s";
}
