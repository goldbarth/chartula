namespace Chartula.Core.Llm;

/// <summary>
/// Counts the requests the transport sends for one model call.
/// Provider clients retry on their own, for example after an overload, a rate limit
/// or a timeout. A retried call still returns as one call with one token count.
/// Without this count, a run slowed by retries looks the same as a slow model.
/// <para>
/// The count flows with the call's async context, so it needs no provider type.
/// The composition root puts a handler under each client that reports every request
/// here. A client without that handler reports nothing, which reads as "not observed",
/// not as "no retries".
/// </para>
/// </summary>
public sealed class ModelCallAttempts : IDisposable
{
    private static readonly AsyncLocal<ModelCallAttempts?> CurrentCall = new();

    private readonly ModelCallAttempts? _outer;
    private int _count;

    private ModelCallAttempts()
    {
        _outer = CurrentCall.Value;
        CurrentCall.Value = this;
    }

    /// <summary>Starts counting for the model call about to be made.</summary>
    public static ModelCallAttempts Begin() => new();

    /// <summary>Called by the transport for every request it sends. Outside a model call it does nothing.</summary>
    public static void RecordRequest()
    {
        if (CurrentCall.Value is { } current)
        {
            Interlocked.Increment(ref current._count);
        }
    }

    /// <summary>The requests sent for this call, or <c>null</c> when the transport reported none.</summary>
    public int? Observed => Volatile.Read(ref _count) is > 0 and var count ? count : null;

    public void Dispose() => CurrentCall.Value = _outer;
}
