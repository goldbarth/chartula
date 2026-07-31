namespace Chartula.Core.Llm;

/// <summary>How a faithfulness check ended.</summary>
public enum FaithfulnessCheckStatus
{
    /// <summary>
    /// The check ran and produced a verdict. <see cref="FaithfulnessReport.UnsupportedClaims"/>
    /// carries it, and an empty list here means the output is clean.
    /// </summary>
    Checked,

    /// <summary>
    /// The check ran but its answer could not be used, so nothing was verified. This is
    /// not a clean result: it says the output is unchecked, not that it is faithful.
    /// </summary>
    NotEvaluated,

    /// <summary>The check did not run: turned off, or nothing to check.</summary>
    Skipped,
}

/// <summary>
/// The result of a faithfulness check: how the check ended, and any claims the facts
/// do not back.
/// </summary>
/// <remarks>
/// The status exists because "clean" and "could not be checked" are different answers
/// that used to look the same. Keep them apart at every call site: a caller that reads
/// only <see cref="UnsupportedClaims"/> reads an empty list in both cases.
/// </remarks>
/// <param name="Status">How the check ended.</param>
/// <param name="UnsupportedClaims">
/// Claims found in the output that the facts do not back. Always empty unless
/// <see cref="Status"/> is <see cref="FaithfulnessCheckStatus.Checked"/>.
/// </param>
/// <param name="Reason">
/// Why the check could not be evaluated; null for every other status.
/// </param>
public sealed record FaithfulnessReport(
    FaithfulnessCheckStatus Status,
    IReadOnlyList<string> UnsupportedClaims,
    string? Reason = null)
{
    /// <summary>The check ran; these are its findings, empty when the output is clean.</summary>
    public static FaithfulnessReport Checked(IReadOnlyList<string> unsupportedClaims)
        => new(FaithfulnessCheckStatus.Checked, unsupportedClaims);

    /// <summary>The check ran but could not be read. <paramref name="reason"/> says why.</summary>
    public static FaithfulnessReport NotEvaluated(string reason)
        => new(FaithfulnessCheckStatus.NotEvaluated, [], reason);

    /// <summary>The check was not run at all.</summary>
    public static FaithfulnessReport Skipped { get; } = new(FaithfulnessCheckStatus.Skipped, []);

    /// <summary>Whether the check flagged anything. False for a check that never produced a verdict.</summary>
    public bool HasFindings => UnsupportedClaims.Count > 0;
}
