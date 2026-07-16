using Chartula.Core.Llm;

namespace Chartula.Core.Tests.Faithfulness;

/// <summary>
/// A fake <see cref="IChangelogModel"/> for the thorough check: records whether
/// the faithfulness pass was called and returns a canned report. No live provider.
/// </summary>
internal sealed class StubFaithfulnessModel(FaithfulnessReport report) : IChangelogModel
{
    public int CheckCallCount { get; private set; }

    public FaithfulnessRequest? LastRequest { get; private set; }

    public Task<string> RephraseAsync(RephraseRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Not exercised by faithfulness tests.");

    public Task<FaithfulnessReport> CheckFaithfulnessAsync(
        FaithfulnessRequest request, CancellationToken cancellationToken = default)
    {
        CheckCallCount++;
        LastRequest = request;
        return Task.FromResult(report);
    }
}
