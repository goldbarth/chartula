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
    public static ChangelogGenerationResult Success(string text) => new(true, text, null);

    public static ChangelogGenerationResult Failure(string error) => new(false, null, error);
}
