using Chartula.Core.Llm;

namespace Chartula.Cli.Composition;

/// <summary>
/// Sits under a provider client and reports every request it sends, so the run can
/// say how often a model call was retried.
/// Both provider SDKs retry through the <see cref="HttpClient"/> they are given. So
/// this handler sees every attempt without knowing the provider.
/// </summary>
/// <param name="inner">The handler that sends; the platform's own unless a test puts a stub there.</param>
internal sealed class ModelRequestCountingHandler(HttpMessageHandler? inner = null)
    : DelegatingHandler(inner ?? new SocketsHttpHandler())
{
    /// <summary>
    /// A client for a provider SDK.
    /// Its timeout is infinite, like the clients the SDKs build for themselves. The SDKs
    /// enforce their own per-request timeout, and the <see cref="HttpClient"/> default
    /// of 100 seconds would cut off a long call first.
    /// <see cref="ModelErrorResponseHandler"/> sits below the counter, so a failed call
    /// can report the error response it received.
    /// </summary>
    public static HttpClient CreateClient(HttpMessageHandler? inner = null)
        => new(new ModelRequestCountingHandler(new ModelErrorResponseHandler(inner))) { Timeout = Timeout.InfiniteTimeSpan };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ModelCallAttempts.RecordRequest();
        return base.SendAsync(request, cancellationToken);
    }
}
