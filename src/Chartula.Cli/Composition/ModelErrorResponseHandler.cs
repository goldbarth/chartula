namespace Chartula.Cli.Composition;

/// <summary>
/// Sits under a provider client and keeps the last error response of the model call
/// in progress, for <see cref="FailureDescribingChatClient"/> to name. Like the
/// request count, it flows with the call's async context: the transport sees every
/// response without knowing which provider it is, and the call it belongs to is the
/// one that asked. Outside a call it only passes responses through.
/// </summary>
/// <param name="inner">The handler that sends; the platform's own unless a test puts a stub there.</param>
internal sealed class ModelErrorResponseHandler(HttpMessageHandler? inner = null)
    : DelegatingHandler(inner ?? new SocketsHttpHandler())
{
    private static readonly AsyncLocal<Capture?> CurrentCall = new();

    /// <summary>Starts keeping error responses for the model call about to be made.</summary>
    public static Capture Begin() => new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode && CurrentCall.Value is { } capture)
        {
            // The last one wins: after a retried call it is the answer the SDK gave up on.
            capture.Last = await ModelErrorResponse.ReadAsync(request, response, cancellationToken);
        }

        return response;
    }

    /// <summary>The error responses of one model call; disposing it ends the call.</summary>
    public sealed class Capture : IDisposable
    {
        private readonly Capture? _outer;
        private ModelErrorResponse? _last;

        internal Capture()
        {
            _outer = CurrentCall.Value;
            CurrentCall.Value = this;
        }

        /// <summary>The last error response the call received, or <c>null</c> when it received none.</summary>
        public ModelErrorResponse? Last
        {
            get => Volatile.Read(ref _last);
            internal set => Volatile.Write(ref _last, value);
        }

        public void Dispose() => CurrentCall.Value = _outer;
    }
}
