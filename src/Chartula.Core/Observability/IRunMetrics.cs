namespace Chartula.Core.Observability;

/// <summary>
/// Collects what a run did and what it cost. Recording is a side channel: it never
/// changes what the pipeline produces, so a metrics sink that does nothing is a valid
/// implementation.
/// </summary>
public interface IRunMetrics
{
    /// <summary>
    /// Records one LLM call and the tokens it consumed. A null count means the provider did not
    /// report that side of the usage. Any call missing either side is tallied separately, so a
    /// low token total can be told apart from a cheap run.
    /// </summary>
    void RecordLlmCall(LlmOperation operation, long? inputTokens, long? outputTokens);

    /// <summary>
    /// Records one pass of both faithfulness checks over the same text. Passing both
    /// findings together is what lets the report tell which claims only the thorough
    /// check caught.
    /// </summary>
    /// <param name="ruleBasedFlags">What the free check flagged.</param>
    /// <param name="thoroughFlags">What the thorough check flagged.</param>
    /// <param name="thoroughEvaluated">
    /// False when the thorough check ran but produced no usable answer. Counted apart from
    /// findings, because a check that could not be read has not cleared the text.
    /// </param>
    void RecordFaithfulnessChecks(
        IReadOnlyCollection<string> ruleBasedFlags,
        IReadOnlyCollection<string> thoroughFlags,
        bool thoroughEvaluated);

    /// <summary>The report as it stands.</summary>
    RunReport Snapshot();
}
