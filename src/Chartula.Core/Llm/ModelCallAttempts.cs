namespace Chartula.Core.Llm;

/// <summary>
/// Counts the requests the transport sends for one model call. Provider clients
/// retry on their own - after an overload, a rate limit, a timeout - and a retried
/// call returns as one call with one token count, so without this a slow run cannot
/// be told apart from a slow model. The count flows with the call's async context,
/// so it needs no provider type: the composition root puts a handler under each
/// client that reports every request here, and a client without one reports nothing,
/// which reads as "not observed" rather than as no retries.
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

    /// <summary>Called by the transport for every request it sends; outside a model call it does nothing.</summary>
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
