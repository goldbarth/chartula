namespace Chartula.Core.Llm;

/// <summary>
/// The audience a changelog entry is tailored for. Chartula renders the same
/// grounded facts once per audience, so the wording differs but the underlying
/// facts never can.
/// </summary>
public enum Audience
{
    /// <summary>Engineers reading for technical detail.</summary>
    Technical,

    /// <summary>Customers reading for what changed for them.</summary>
    Customer,

    /// <summary>Product managers reading for impact and framing.</summary>
    Product,
}
