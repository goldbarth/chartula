namespace Chartula.Core.Llm;

/// <summary>How a faithfulness check ended.</summary>
public enum FaithfulnessCheckStatus
{
    /// <summary>
    /// The check ran and produced a verdict, carried in
    /// <see cref="FaithfulnessReport.UnsupportedClaims"/>.
    /// With this status, an empty list means the output is clean.
    /// </summary>
    Checked,

    /// <summary>
    /// The check ran but its answer could not be used, so nothing was verified.
    /// The output is unchecked, which is different from faithful.
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
/// "Clean" and "could not be checked" are different answers that used to look the
/// same, which is why <see cref="Status"/> exists.
/// Keep them apart at every call site: <see cref="UnsupportedClaims"/> is an empty
/// list in both cases.
/// </remarks>
/// <param name="Status">How the check ended.</param>
/// <param name="UnsupportedClaims">
/// Claims found in the output that the facts do not back, each with the fact it names.
/// Always empty unless <see cref="Status"/> is <see cref="FaithfulnessCheckStatus.Checked"/>.
/// </param>
/// <param name="Reason">
/// Why the check could not be evaluated; null for every other status.
/// </param>
public sealed record FaithfulnessReport(
    FaithfulnessCheckStatus Status,
    IReadOnlyList<FaithfulnessFlag> UnsupportedClaims,
    string? Reason = null)
{
    /// <summary>The check ran; these are its findings, empty when the output is clean.</summary>
    public static FaithfulnessReport Checked(IReadOnlyList<FaithfulnessFlag> unsupportedClaims)
        => new(FaithfulnessCheckStatus.Checked, unsupportedClaims);

    /// <summary>The check ran but could not be read. <paramref name="reason"/> says why.</summary>
    public static FaithfulnessReport NotEvaluated(string reason)
        => new(FaithfulnessCheckStatus.NotEvaluated, [], reason);

    /// <summary>The check was not run at all.</summary>
    public static FaithfulnessReport Skipped { get; } = new(FaithfulnessCheckStatus.Skipped, []);

    /// <summary>Whether the check flagged anything. False for a check that never produced a verdict.</summary>
    public bool HasFindings => UnsupportedClaims.Count > 0;
}
