namespace Chartula.Core.Formatting;

/// <summary>
/// Normalizes a rendered changelog so its formatting is consistent within the
/// document, whatever the model returned.
/// This guarantees the mechanical formatting. Consistent tone is the prompt's job.
/// </summary>
public interface IChangelogFormatter
{
    string Format(string rendered);
}
