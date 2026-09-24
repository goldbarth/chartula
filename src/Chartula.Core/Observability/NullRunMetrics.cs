namespace Chartula.Core.Observability;

/// <summary>
/// An <see cref="IRunMetrics"/> that records nothing.
/// Callers that do not need metrics use it instead of a real sink, so call sites need
/// no null checks.
/// </summary>
public sealed class NullRunMetrics : IRunMetrics
{
    /// <summary>The shared instance.</summary>
    public static NullRunMetrics Instance { get; } = new();

    private NullRunMetrics()
    {
    }

    public void RecordLlmCall(LlmOperation operation, LlmCall call)
    {
    }

    public void RecordRunDuration(TimeSpan duration)
    {
    }

    public void RecordReleaseScope(ReleaseScope scope)
    {
    }

    public void RecordFaithfulnessChecks(
        IReadOnlyCollection<string> ruleBasedFlags,
        IReadOnlyCollection<string> thoroughFlags,
        bool thoroughEvaluated)
    {
    }

    public RunReport Snapshot() => RunReport.Empty;
}
