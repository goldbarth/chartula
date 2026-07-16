namespace Chartula.Core.Observability;

/// <summary>
/// Collects what a run did and what it cost. Recording is a side channel: it never
/// changes what the pipeline produces, so a metrics sink that does nothing is a valid
/// implementation.
/// </summary>
public interface IRunMetrics
{
    /// <summary>Records one LLM call and the tokens it consumed.</summary>
    void RecordLlmCall(LlmOperation operation, TokenUsage usage);

    /// <summary>
    /// Records one pass of both faithfulness checks over the same text. Passing both
    /// findings together is what lets the report tell which claims only the thorough
    /// check caught.
    /// </summary>
    void RecordFaithfulnessChecks(
        IReadOnlyCollection<string> ruleBasedFlags,
        IReadOnlyCollection<string> thoroughFlags);

    /// <summary>The report as it stands.</summary>
    RunReport Snapshot();
}
