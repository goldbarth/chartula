using Chartula.Core.Llm;

namespace Chartula.Core.Tests.Rendering;

/// <summary>
/// A fake <see cref="IChangelogModel"/> that records every rephrase request keyed
/// by audience, so a test can inspect what each audience was actually sent. No
/// live provider is involved.
/// </summary>
internal sealed class RecordingChangelogModel : IChangelogModel
{
    public Dictionary<Audience, RephraseRequest> RequestsByAudience { get; } = [];

    public int RephraseCallCount { get; private set; }

    public Task<string> RephraseAsync(RephraseRequest request, CancellationToken cancellationToken = default)
    {
        RephraseCallCount++;
        RequestsByAudience[request.Audience] = request;
        return Task.FromResult($"[{request.Audience}] rendering");
    }

    public IReadOnlyList<string> StatementsFor(Audience audience)
        => RequestsByAudience[audience].Facts.Statements;

    public Task<FaithfulnessReport> CheckFaithfulnessAsync(
        FaithfulnessRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Not exercised by rendering tests.");
}
