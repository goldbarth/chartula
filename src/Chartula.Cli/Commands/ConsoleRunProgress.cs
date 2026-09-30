using System.Globalization;
using Chartula.Core.Observability;

namespace Chartula.Cli.Commands;

/// <summary>
/// Shows a run's steps on stderr, so a changelog redirected from stdout stays clean (#283).
/// In a terminal the current line updates in place, its elapsed time ticking each second,
/// and a step that ends stays as a line of its own with its final time. Without one, as in
/// CI, each step is one plain line as it starts, with no control characters, so a job log
/// stays readable and shows the step a run was in when it stopped.
/// </summary>
/// <param name="output">Where the steps go: stderr.</param>
/// <param name="interactive">Whether <paramref name="output"/> is a terminal.</param>
/// <param name="time">The clock; the system clock unless a test sets one.</param>
internal sealed class ConsoleRunProgress(TextWriter output, bool interactive, TimeProvider? time = null)
    : IRunProgress, IDisposable
{
    // Wide enough for "Reading pull requests" and "Rendering technical".
    private const int LabelWidth = 24;
    private const int CountWidth = 17;

    // Carriage return, then clear to the end of the line: the line is rewritten in place.
    private const string Rewrite = "\r\u001b[K";

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Lock _lock = new();
    private ITimer? _ticker;
    private string? _label;
    private int? _total;
    private int _done;
    private long _started;

    public void Begin(ProgressStep step, int? total = null)
    {
        lock (_lock)
        {
            EndStep();
            _label = Label(step);
            _total = total;
            _done = 0;
            _started = _time.GetTimestamp();

            if (!interactive)
            {
                output.WriteLine(total is { } count ? $"{_label} ({Commits(count)})" : _label);
                output.Flush();
                return;
            }

            Draw();
            // The elapsed time is the sign a slow step is still working, so it ticks
            // while a model call runs, not only when a count moves.
            _ticker ??= _time.CreateTimer(_ => Tick(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
    }

    public void Advance(int done)
    {
        lock (_lock)
        {
            _done = done;
            if (interactive && _label is not null)
            {
                Draw();
            }
        }
    }

    public void Complete()
    {
        lock (_lock)
        {
            EndStep();
            _ticker?.Dispose();
            _ticker = null;
        }
    }

    public void Dispose() => Complete();

    private void Tick()
    {
        lock (_lock)
        {
            if (_label is not null)
            {
                Draw();
            }
        }
    }

    // A finished step keeps its line, with the time it took.
    private void EndStep()
    {
        if (interactive && _label is not null)
        {
            Draw();
            output.WriteLine();
            output.Flush();
        }

        _label = null;
    }

    private void Draw()
    {
        string count = _total is { } total ? $"{_done}/{Commits(total)}" : string.Empty;
        string elapsed = Elapsed(_time.GetElapsedTime(_started));
        output.Write($"{Rewrite}{_label!.PadRight(LabelWidth)}{count.PadRight(CountWidth)}{elapsed}");
        output.Flush();
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
            : $"{(int)elapsed.TotalMinutes} min {elapsed.Seconds} s";
}
