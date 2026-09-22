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
    private TimeSpan? _runDuration;

    public void RecordLlmCall(LlmOperation operation, LlmCall call)
    {
        ArgumentNullException.ThrowIfNull(call);

        lock (_gate)
        {
            LlmUsage current = _llm.TryGetValue(operation, out LlmUsage? existing) ? existing : LlmUsage.None;

            // A failed call returned no usage to count, but its time was spent.
            bool answered = !call.Failed;
            int unreportedCalls = answered && (call.InputTokens is null || call.OutputTokens is null) ? 1 : 0;

            _llm[operation] = new LlmUsage(
                current.TotalCalls + (answered ? 1 : 0),
                current.CallsWithoutUsage + unreportedCalls,
                current.Tokens + new TokenUsage(call.InputTokens ?? 0, call.OutputTokens ?? 0))
            {
                CachedInputTokens = Add(current.CachedInputTokens, call.CachedInputTokens),
                ReasoningTokens = Add(current.ReasoningTokens, call.ReasoningTokens),
                FailedCalls = current.FailedCalls + (answered ? 0 : 1),
                Duration = current.Duration + call.Duration,
                LongestCall = call.Duration > current.LongestCall ? call.Duration : current.LongestCall,
                Retries = call.Attempts is { } attempts
                    ? (current.Retries ?? 0) + Math.Max(0, attempts - 1)
                    : current.Retries,
            };
        }
    }

    // Unknown stays unknown until a call reports it; after that, calls that did not
    // report add nothing rather than making the known part unknown again.
    private static long? Add(long? sum, long? value) => value is null ? sum : (sum ?? 0) + value;

    public void RecordRunDuration(TimeSpan duration)
    {
        lock (_gate)
        {
            _runDuration = duration;
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
                new Dictionary<LlmOperation, LlmUsage>(_llm))
            {
                Duration = _runDuration,
            };
        }
    }
}
