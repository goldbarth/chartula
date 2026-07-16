namespace Chartula.Core.Observability;

/// <summary>
/// An <see cref="IRunMetrics"/> that records nothing. Lets callers that do not care
/// about measurement stay free of a sink, without any null checks at the call sites.
/// </summary>
public sealed class NullRunMetrics : IRunMetrics
{
    /// <summary>The shared instance.</summary>
    public static NullRunMetrics Instance { get; } = new();

    private NullRunMetrics()
    {
    }

    public void RecordLlmCall(LlmOperation operation, TokenUsage usage)
    {
    }

    public void RecordFaithfulnessChecks(
        IReadOnlyCollection<string> ruleBasedFlags,
        IReadOnlyCollection<string> thoroughFlags)
    {
    }

    public RunReport Snapshot() => RunReport.Empty;
}
