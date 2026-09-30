using Chartula.Core.History;

namespace Chartula.Core.Pipeline;

/// <summary>
/// Where the operator sees the range of a run and confirms a large one, before any
/// GitHub request or model call.
/// It is a port because asking needs a terminal, which the domain does not have.
/// Without a gate, a range <see cref="LargeRangeRule"/> applies to counts as declined.
/// </summary>
public interface IReleaseRangeGate
{
    /// <summary>Shows the range the run is about to read. Called for every run.</summary>
    void Announce(CommitRange range);

    /// <summary>
    /// Asks whether a range <see cref="LargeRangeRule"/> applies to is read.
    /// Returns false when the operator declines or there is no one to ask.
    /// </summary>
    Task<bool> ConfirmAsync(CommitRange range, CancellationToken cancellationToken = default);
}
