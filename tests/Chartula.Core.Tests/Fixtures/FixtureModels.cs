using Chartula.Core.Facts;
using Chartula.Core.History;
using Chartula.Core.Llm;
using Chartula.Core.PullRequests;

namespace Chartula.Core.Tests.Fixtures;

/// <summary>Hands the pipeline a stored fact base instead of building one from a repository.</summary>
internal sealed class FixtureFactBaseBuilder(FactBase factBase) : IFactBaseBuilder
{
    public FactBase Build(CommitRange range, IReadOnlyList<PullRequestInfo> pullRequests) => factBase;
}

/// <summary>
/// A stand-in <see cref="IChangelogModel"/> that rephrases by echoing the grounded facts
/// back as a list. It invents nothing, so a faithful run over a fixture is faithful by
/// construction - and any flag a test sees is the checker's doing, not the model's.
/// </summary>
internal sealed class EchoingChangelogModel : IChangelogModel
{
    public int RephraseCalls { get; private set; }

    public int CheckCalls { get; private set; }

    public Task<string> RephraseAsync(RephraseRequest request, CancellationToken cancellationToken = default)
    {
        RephraseCalls++;
        return Task.FromResult(string.Join('\n', request.Facts.Statements.Select(static s => $"- {s}")));
    }

    public Task<FaithfulnessReport> CheckFaithfulnessAsync(
        FaithfulnessRequest request, CancellationToken cancellationToken = default)
    {
        CheckCalls++;
        return Task.FromResult(new FaithfulnessReport(IsFaithful: true, UnsupportedClaims: []));
    }
}

/// <summary>
/// A stand-in model that invents a fact. It exists so tests can prove the checks still
/// bite when replaying a fixture, rather than only proving that clean input stays clean.
/// </summary>
internal sealed class InventingChangelogModel : IChangelogModel
{
    public Task<string> RephraseAsync(RephraseRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult("- Rewrote the `QuantumScheduler` across 9,001 modules.");

    public Task<FaithfulnessReport> CheckFaithfulnessAsync(
        FaithfulnessRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new FaithfulnessReport(IsFaithful: true, UnsupportedClaims: []));
}

/// <summary>
/// A model that fails the test if it is ever reached. It turns "this path costs no
/// tokens" from a claim into something the suite enforces.
/// </summary>
internal sealed class UnreachableChangelogModel : IChangelogModel
{
    public Task<string> RephraseAsync(RephraseRequest request, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("The model was called on a path that must not call it.");

    public Task<FaithfulnessReport> CheckFaithfulnessAsync(
        FaithfulnessRequest request, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("The model was called on a path that must not call it.");
}
