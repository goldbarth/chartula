namespace Chartula.Core.Pipeline;

/// <summary>
/// A run stopped because its range was not confirmed.
/// It is a separate type so the CLI can tell a declined range from a failure; nothing
/// was spent on it, which is the point of asking.
/// </summary>
public sealed class UnconfirmedRangeException(string tag)
    : InvalidOperationException(
        $"The range of {tag} was not confirmed, so nothing was read from GitHub or sent to the model.")
{
    /// <summary>The release tag.</summary>
    public string Tag { get; } = tag;
}
