namespace Chartula.Core.History;

/// <summary>
/// A single commit belonging to a release, reduced to what the curation and
/// fallback steps need. Enriched by later issues (e.g. author, body) as those
/// steps require more.
/// </summary>
/// <param name="Sha">The full commit hash.</param>
/// <param name="Subject">The commit subject (first line of the message).</param>
public sealed record CommitInfo(string Sha, string Subject);
