namespace Chartula.Core.Generation;

/// <summary>
/// The outcome of a generation call: the produced text on success, or a clear
/// error message on failure. Provider failures are surfaced as a failed result
/// rather than an exception, so one bad release never crashes the pipeline.
/// </summary>
/// <param name="IsSuccess">Whether generation succeeded.</param>
/// <param name="Text">The generated changelog text, or <c>null</c> on failure.</param>
/// <param name="Error">A clear error message, or <c>null</c> on success.</param>
public sealed record ChangelogGenerationResult(bool IsSuccess, string? Text, string? Error)
{
    /// <summary>
    /// The one-sentence summary of the release, written in the same call as the
    /// text, or <c>null</c> when the audience has no such field or the model
    /// could not write one from the facts. Only the customer rendering carries
    /// one; a field with no source is omitted rather than emitted empty.
    /// </summary>
    public string? Description { get; init; }

    public static ChangelogGenerationResult Success(string text) => new(true, text, null);

    public static ChangelogGenerationResult Success(string text, string? description)
        => new(true, text, null) { Description = description };

    public static ChangelogGenerationResult Failure(string error) => new(false, null, error);
}
