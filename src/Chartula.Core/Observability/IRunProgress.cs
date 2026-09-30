using Chartula.Core.Llm;

namespace Chartula.Core.Observability;

/// <summary>
/// Shows a run's steps while it works, so a slow run can be told from a hanging one (#283).
/// A run takes from seconds to minutes, and everything else it reports comes at the end.
/// A port because showing needs a console, which the domain does not have.
/// The steps are the ones the run already has, in the order it takes them; no estimate
/// of the time remaining, since a model call's duration is not known before it returns.
/// </summary>
public interface IRunProgress
{
    /// <summary>Starts <paramref name="step"/>, which ends the one before it.</summary>
    /// <param name="step">The step.</param>
    /// <param name="total">How many units the step has, when it has a count.</param>
    void Begin(ProgressStep step, int? total = null);

    /// <summary>How many units of the current step are done.</summary>
    void Advance(int done);

    /// <summary>
    /// Ends the current step, also when the run failed in it, so it stays the last line
    /// reached and the error that follows can be placed.
    /// </summary>
    void Complete();
}

/// <summary>The kinds of step a run takes.</summary>
public enum RunStep
{
    /// <summary>Asking GitHub for the pull request behind each commit, one request per commit.</summary>
    ReadingPullRequests,

    /// <summary>Rendering one audience, one model call.</summary>
    Rendering,

    /// <summary>Checking one audience's rendering: the rule-based check and, when on, the thorough check.</summary>
    Checking,
}

/// <summary>One step of a run.</summary>
/// <param name="Kind">What the step does.</param>
/// <param name="Audience">The audience it is for, or <c>null</c> for a step that is for none.</param>
public sealed record ProgressStep(RunStep Kind, Audience? Audience = null);

/// <summary>Shows nothing: a run without a console, such as a test.</summary>
public sealed class NullRunProgress : IRunProgress
{
    public static NullRunProgress Instance { get; } = new();

    public void Begin(ProgressStep step, int? total = null)
    {
    }

    public void Advance(int done)
    {
    }

    public void Complete()
    {
    }
}
