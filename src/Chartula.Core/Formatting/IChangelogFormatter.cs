namespace Chartula.Core.Formatting;

/// <summary>
/// Normalizes a rendered changelog so its formatting is consistent within the
/// document, regardless of what the model returned. Tone normalization is the
/// prompt's job; this guarantees the mechanical formatting.
/// </summary>
public interface IChangelogFormatter
{
    string Format(string rendered);
}
