namespace Chartula.Core.Llm;

/// <summary>
/// The shape the model fills in for the thorough check, and the schema its response is
/// held to.
/// </summary>
/// <remarks>
/// Deliberately not <see cref="FaithfulnessReport"/>. This is the model's answer; the
/// report is what the pipeline acts on, and it has to express something a model can
/// never report about itself - that no usable answer came back at all.
/// </remarks>
/// <param name="IsFaithful">The model's verdict: true when it found every claim supported.</param>
/// <param name="UnsupportedClaims">The claims it found unsupported. May be absent.</param>
public sealed record FaithfulnessVerdict(bool IsFaithful, IReadOnlyList<string>? UnsupportedClaims);
