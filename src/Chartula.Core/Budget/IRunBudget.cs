namespace Chartula.Core.Budget;

/// <summary>
/// Decides whether a run may spend what it is estimated to spend. Called once,
/// after the fact base is built and before the first model call, so a refusal
/// costs no tokens. What a token costs and how much is too much are the command
/// surface's to know; the domain only knows how many tokens a run can use.
/// </summary>
public interface IRunBudget
{
    /// <summary>Returns when the run may go ahead.</summary>
    /// <exception cref="InvalidOperationException">The run may not spend this much.</exception>
    void Approve(RunEstimate estimate);
}
