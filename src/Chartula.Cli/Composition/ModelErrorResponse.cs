using System.Net;
using System.Text.Json;

namespace Chartula.Cli.Composition;

/// <summary>
/// An error response a model endpoint sent, as it came off the wire. Both provider
/// SDKs turn one into an exception of their own type and in their own words -
/// <c>Status Code: NotFound</c> from one, <c>HTTP 404 (...)</c> from the other - and
/// neither names the address it asked. Read below both SDKs, the address, the status
/// and the endpoint's own message come out the same way for either, and no provider
/// type is named.
/// </summary>
/// <param name="Endpoint">The address the request went to, without query or credentials.</param>
/// <param name="StatusCode">The status, for deciding what it means.</param>
/// <param name="Status">The status as it is shown, e.g. <c>404 Not Found</c>.</param>
/// <param name="Message">The endpoint's own message, or <c>null</c> when the body had none.</param>
internal sealed record ModelErrorResponse(string Endpoint, HttpStatusCode StatusCode, string Status, string? Message)
{
    private const int MaxMessageLength = 300;

    /// <summary>Reads what <paramref name="response"/> says, leaving its body readable for the SDK.</summary>
    public static async Task<ModelErrorResponse> ReadAsync(
        HttpRequestMessage request, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        // Buffered first: the SDK reads the body after this, and a stream read twice is empty.
        await response.Content.LoadIntoBufferAsync(cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);

        string reason = response.ReasonPhrase is { Length: > 0 } phrase ? phrase : response.StatusCode.ToString();
        return new ModelErrorResponse(
            EndpointOf(request.RequestUri),
            response.StatusCode,
            $"{(int)response.StatusCode} {reason}",
            MessageOf(body));
    }

    // Scheme, host and path only: a query string can carry a key (some providers take
    // one there), and so can user info.
    private static string EndpointOf(Uri? uri)
        => uri is null ? "an unknown address" : $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}";

    /// <summary>
    /// The message in the body. Anthropic and OpenAI both put it at <c>error.message</c>;
    /// the other shapes are what servers speaking the OpenAI dialect send instead.
    /// </summary>
    private static string? MessageOf(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            if (FindMessage(document.RootElement) is { } message)
            {
                return SingleLine(message);
            }
        }
        catch (JsonException)
        {
            // Not JSON (a proxy's HTML page, say); the body is all there is.
        }

        return SingleLine(body);
    }

    private static string? FindMessage(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (root.TryGetProperty("error", out JsonElement error))
        {
            if (error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("message", out JsonElement nested)
                && nested.ValueKind == JsonValueKind.String)
            {
                return nested.GetString();
            }

            if (error.ValueKind == JsonValueKind.String)
            {
                return error.GetString();
            }
        }

        foreach (string name in (string[])["message", "detail"])
        {
            if (root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static string? SingleLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string line = string.Join(' ', text.Split((char[])['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return line.Length <= MaxMessageLength ? line : line[..MaxMessageLength] + "...";
    }
}
