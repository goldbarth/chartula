using Chartula.Core.Llm;

namespace Chartula.Core.Tests.Generation;

/// <summary>
/// A fake <see cref="IChangelogModel"/> for driving the generator without a live
/// provider: records calls, returns a canned rephrase, or throws on demand.
/// </summary>
internal sealed class FakeChangelogModel : IChangelogModel
{
    private readonly string _response;
    private readonly Exception? _throw;

    private FakeChangelogModel(string response, Exception? toThrow)
    {
        _response = response;
        _throw = toThrow;
    }

    public int RephraseCallCount { get; private set; }

    public RephraseRequest? LastRequest { get; private set; }

    public static FakeChangelogModel Returning(string response) => new(response, null);

    public static FakeChangelogModel Throwing(Exception toThrow) => new(string.Empty, toThrow);

    public Task<string> RephraseAsync(RephraseRequest request, CancellationToken cancellationToken = default)
    {
        RephraseCallCount++;
        LastRequest = request;
        cancellationToken.ThrowIfCancellationRequested();
        return _throw is not null ? throw _throw : Task.FromResult(_response);
    }

    public Task<FaithfulnessReport> CheckFaithfulnessAsync(
        FaithfulnessRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Not exercised by generation tests.");
}
