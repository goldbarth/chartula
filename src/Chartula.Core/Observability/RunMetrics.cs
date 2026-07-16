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

    public void RecordLlmCall(LlmOperation operation, TokenUsage usage)
    {
        lock (_gate)
        {
            LlmUsage current = _llm.TryGetValue(operation, out LlmUsage? existing) ? existing : LlmUsage.None;
            _llm[operation] = new LlmUsage(current.Calls + 1, current.Tokens + usage);
        }
    }

    public void RecordFaithfulnessChecks(
        IReadOnlyCollection<string> ruleBasedFlags,
        IReadOnlyCollection<string> thoroughFlags)
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
                _thoroughOnlyFlags,
                new Dictionary<LlmOperation, LlmUsage>(_llm));
        }
    }
}
