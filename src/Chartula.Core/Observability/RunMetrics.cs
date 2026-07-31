namespace Chartula.Core.Observability;

/// <summary>
/// Default <see cref="IRunMetrics"/>. Accumulates one run's activity in memory. Audiences
/// may be rendered concurrently, so recording is guarded by a lock.
/// </summary>
public sealed class RunMetrics : IRunMetrics
{
    private readonly Lock _gate = new();
    private readonly Dictionary<LlmOperation, LlmUsage> _llm = [];

    private int _ruleBasedRuns;
    private int _ruleBasedRunsWithFindings;
    private int _ruleBasedFlags;
    private int _thoroughRuns;
    private int _thoroughRunsWithFindings;
    private int _thoroughFlags;
    private int _thoroughOnlyFlags;
    private int _thoroughNotEvaluated;

    public void RecordLlmCall(LlmOperation operation, long? inputTokens, long? outputTokens)
    {
        lock (_gate)
        {
            int unreportedCalls = inputTokens is null || outputTokens is null ? 1 : 0;
            LlmUsage current = _llm.TryGetValue(operation, out LlmUsage? existing) ? existing : LlmUsage.None;
            _llm[operation] = new LlmUsage(
                current.TotalCalls + 1,
                current.CallsWithoutUsage + unreportedCalls,
                current.Tokens + new TokenUsage(inputTokens ?? 0, outputTokens ?? 0));
        }
    }

    public void RecordFaithfulnessChecks(
        IReadOnlyCollection<string> ruleBasedFlags,
        IReadOnlyCollection<string> thoroughFlags,
        bool thoroughEvaluated)
    {
        ArgumentNullException.ThrowIfNull(ruleBasedFlags);
        ArgumentNullException.ThrowIfNull(thoroughFlags);

        // What the thorough check caught that the free check did not - the reason to
        // pay for it at all.
        int onlyThorough = thoroughFlags.Except(ruleBasedFlags, StringComparer.Ordinal).Count();

        lock (_gate)
        {
            _ruleBasedRuns++;
            _ruleBasedFlags += ruleBasedFlags.Count;
            if (ruleBasedFlags.Count > 0)
            {
                _ruleBasedRunsWithFindings++;
            }

            _thoroughRuns++;
            _thoroughFlags += thoroughFlags.Count;
            if (thoroughFlags.Count > 0)
            {
                _thoroughRunsWithFindings++;
            }

            if (!thoroughEvaluated)
            {
                _thoroughNotEvaluated++;
            }

            _thoroughOnlyFlags += onlyThorough;
        }
    }

    public RunReport Snapshot()
    {
        lock (_gate)
        {
            return new RunReport(
                new CheckActivity(_ruleBasedRuns, _ruleBasedRunsWithFindings, _ruleBasedFlags),
                new CheckActivity(_thoroughRuns, _thoroughRunsWithFindings, _thoroughFlags),
                _thoroughNotEvaluated,
                _thoroughOnlyFlags,
                new Dictionary<LlmOperation, LlmUsage>(_llm));
        }
    }
}
