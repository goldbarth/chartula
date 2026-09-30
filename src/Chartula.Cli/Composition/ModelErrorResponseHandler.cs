namespace Chartula.Cli.Composition;

/// <summary>
/// Sits under a provider client and keeps the last response of the current model call,
/// so <see cref="FailureDescribingChatClient"/> can report it: an error response, or an
/// answer the SDK then failed to read.
/// Like the request count, it flows with the call's async context. The transport sees
/// every response without knowing the provider, and the response belongs to the call
/// that sent the request.
/// Outside a model call it only passes responses through.
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
        if (CurrentCall.Value is not { } capture)
        {
            return response;
        }

        // Keep the last response only: after retries, it is the one the SDK gave up on.
        if (response.IsSuccessStatusCode)
        {
            capture.Answered(new ModelAnswer(ModelErrorResponse.EndpointOf(request.RequestUri), ModelErrorResponse.StatusOf(response)));
        }
        else
        {
            capture.Failed(await ModelErrorResponse.ReadAsync(request, response, cancellationToken));
        }

        return response;
    }

    /// <summary>The error responses of one model call. Disposing it ends the call.</summary>
    public sealed class Capture : IDisposable
    {
        private readonly Capture? _outer;
        private ModelErrorResponse? _last;
        private ModelAnswer? _answer;

        internal Capture()
        {
            _outer = CurrentCall.Value;
            CurrentCall.Value = this;
        }

        /// <summary>The call's last response when it was an error, otherwise <c>null</c>.</summary>
        public ModelErrorResponse? Last => Volatile.Read(ref _last);

        /// <summary>
        /// The call's last response when it was a success, otherwise <c>null</c>.
        /// A call that fails with one got an answer the SDK could not read, which is not a
        /// network problem.
        /// </summary>
        public ModelAnswer? Answer => Volatile.Read(ref _answer);

        internal void Failed(ModelErrorResponse error)
        {
            Volatile.Write(ref _answer, null);
            Volatile.Write(ref _last, error);
        }

        internal void Answered(ModelAnswer answer)
        {
            Volatile.Write(ref _last, null);
            Volatile.Write(ref _answer, answer);
        }

        public void Dispose() => CurrentCall.Value = _outer;
    }
}

/// <summary>A successful response from a model endpoint: where it came from and its status.</summary>
/// <param name="Endpoint">The address the request went to, without query or credentials.</param>
/// <param name="Status">The status as it is shown, e.g. <c>200 OK</c>.</param>
internal sealed record ModelAnswer(string Endpoint, string Status);
