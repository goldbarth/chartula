using Chartula.Core.Facts;
using Chartula.Core.History;
using Chartula.Core.Llm;
using Chartula.Core.PullRequests;
using Chartula.Core.Tests.Generation;

namespace Chartula.Core.Tests.Fixtures;

/// <summary>Hands the pipeline a stored fact base instead of building one from a repository.</summary>
internal sealed class FixtureFactBaseBuilder(FactBase factBase) : IFactBaseBuilder
{
    public FactBase Build(CommitRange range, IReadOnlyList<PullRequestInfo> pullRequests) => factBase;
}

/// <summary>
/// A stand-in <see cref="IChangelogModel"/> that rephrases by echoing each grounded fact
/// back as the text of its entry. It invents nothing, so a faithful run over a fixture is faithful by
/// construction - and any flag a test sees is the checker's doing, not the model's.
/// </summary>
internal sealed class EchoingChangelogModel : IChangelogModel
{
    public int RephraseCalls { get; private set; }

    public int CheckCalls => _checkRequests.Count;

    /// <summary>What the thorough check was handed, one request per checked rendering.</summary>
    public IReadOnlyList<FaithfulnessRequest> CheckRequests => _checkRequests;

    private readonly List<FaithfulnessRequest> _checkRequests = [];

    public Task<RenderedEntries> RephraseAsync(RephraseRequest request, CancellationToken cancellationToken = default)
    {
        RephraseCalls++;
        return Task.FromResult(Entries.Echo(request.Facts));
    }

    public Task<FaithfulnessReport> CheckFaithfulnessAsync(
        FaithfulnessRequest request, CancellationToken cancellationToken = default)
    {
        _checkRequests.Add(request);
        return Task.FromResult(FaithfulnessReport.Checked([]));
    }
}

/// <summary>
/// A stand-in model that invents a fact. It exists so tests can prove the checks still
/// bite when replaying a fixture, rather than only proving that clean input stays clean.
/// </summary>
internal sealed class InventingChangelogModel : IChangelogModel
{
    public Task<RenderedEntries> RephraseAsync(RephraseRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(Entries.Each(request.Facts, (_, _) => "Rewrote the `QuantumScheduler` across 9,001 modules."));

    public Task<FaithfulnessReport> CheckFaithfulnessAsync(
        FaithfulnessRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(FaithfulnessReport.Checked([]));
}

/// <summary>
/// A stand-in model that answers every fact with the same text, and finds nothing to flag.
/// It exists to push a given piece of model output through the real composer and writers.
/// </summary>
internal sealed class WritingChangelogModel(string text) : IChangelogModel
{
    public Task<RenderedEntries> RephraseAsync(RephraseRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(Entries.Each(request.Facts, (_, _) => text));

    public Task<FaithfulnessReport> CheckFaithfulnessAsync(
        FaithfulnessRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(FaithfulnessReport.Checked([]));
}

/// <summary>
/// A model that fails the test if it is ever reached. It turns "this path costs no
/// tokens" from a claim into something the suite enforces.
/// </summary>
internal sealed class UnreachableChangelogModel : IChangelogModel
{
    public Task<RenderedEntries> RephraseAsync(RephraseRequest request, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("The model was called on a path that must not call it.");

    public Task<FaithfulnessReport> CheckFaithfulnessAsync(
        FaithfulnessRequest request, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("The model was called on a path that must not call it.");
}
