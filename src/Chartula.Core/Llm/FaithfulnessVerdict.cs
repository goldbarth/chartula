namespace Chartula.Core.Llm;

/// <summary>
/// The shape the model fills in for the thorough check. It is also the schema the
/// model's response must follow.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="FaithfulnessReport"/>. This type is the
/// model's answer. The report is what the pipeline acts on, and it has to express one
/// thing a model can never report about itself: that no usable answer came back.
/// </remarks>
/// <param name="IsFaithful">The model's verdict: true when it found every claim supported.</param>
/// <param name="UnsupportedClaims">The claims it found unsupported. May be absent.</param>
public sealed record FaithfulnessVerdict(bool IsFaithful, IReadOnlyList<UnsupportedClaim>? UnsupportedClaims);

/// <summary>One claim the model found unsupported, and the fact it names for it.</summary>
/// <param name="Claim">The claim and why the facts do not back it.</param>
/// <param name="PullRequest">
/// The pull request number of the fact the claim rephrases, as the model read it.
/// Unverified: the model can name a number no fact has, such as an issue a title mentions.
/// </param>
public sealed record UnsupportedClaim(string Claim, int? PullRequest = null);
