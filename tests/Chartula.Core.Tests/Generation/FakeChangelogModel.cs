using Chartula.Core.Llm;

namespace Chartula.Core.Tests.Generation;

/// <summary>
/// A fake <see cref="IChangelogModel"/> that drives the generator without a live provider.
/// It records calls, answers from the request it received, or throws on demand.
/// </summary>
internal sealed class FakeChangelogModel : IChangelogModel
{
    private readonly Func<RephraseRequest, RenderedEntries> _answer;
    private readonly Exception? _throw;

    private FakeChangelogModel(Func<RephraseRequest, RenderedEntries> answer, Exception? toThrow)
    {
        _answer = answer;
        _throw = toThrow;
    }

    public int RephraseCallCount { get; private set; }

    public RephraseRequest? LastRequest { get; private set; }

    /// <summary>Answers every fact with its own statement.</summary>
    public static FakeChangelogModel Echoing(string? description = null)
        => new(request => Entries.Echo(request.Facts, description), null);

    /// <summary>Answers with whatever <paramref name="answer"/> builds from the request.</summary>
    public static FakeChangelogModel Answering(Func<RephraseRequest, RenderedEntries> answer) => new(answer, null);

    public static FakeChangelogModel Throwing(Exception toThrow) => new(_ => new RenderedEntries([]), toThrow);

    public Task<RenderedEntries> RephraseAsync(RephraseRequest request, CancellationToken cancellationToken = default)
    {
        RephraseCallCount++;
        LastRequest = request;
        cancellationToken.ThrowIfCancellationRequested();
        return _throw is not null ? throw _throw : Task.FromResult(_answer(request));
    }

    public Task<FaithfulnessReport> CheckFaithfulnessAsync(
        FaithfulnessRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Not exercised by generation tests.");
}
