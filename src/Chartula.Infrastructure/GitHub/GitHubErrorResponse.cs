using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chartula.Infrastructure.GitHub;

/// <summary>
/// Reads an error response from the GitHub API, the same way for every GitHub adapter:
/// <list type="bullet">
/// <item>GitHub's own message instead of the raw body,</item>
/// <item>whether the request carried a token,</item>
/// <item>the permission GitHub names as missing,</item>
/// <item>whether the rate limit was the reason, not access.</item>
/// </list>
/// </summary>
internal sealed class GitHubErrorResponse
{
    private GitHubErrorResponse(HttpResponseMessage response, bool withToken, string message)
    {
        StatusCode = response.StatusCode;
        Status = $"{(int)response.StatusCode} {response.ReasonPhrase}";
        WithToken = withToken;
        Message = message;
        NeededPermission = Header(response, "x-accepted-github-permissions");

        // An exhausted rate limit returns 403 or 429, but says nothing about access.
        RateLimited = response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests
                      && Header(response, "x-ratelimit-remaining") == "0";
    }

    public HttpStatusCode StatusCode { get; }

    /// <summary>The status as it is shown, e.g. <c>404 Not Found</c>.</summary>
    public string Status { get; }

    public bool WithToken { get; }

    /// <summary>GitHub's own message, or the body itself when it has none.</summary>
    public string Message { get; }

    /// <summary>The permission GitHub says a fine-grained token is missing, or <c>null</c>.</summary>
    public string? NeededPermission { get; }

    public bool RateLimited { get; }

    public static async Task<GitHubErrorResponse> ReadAsync(
        HttpResponseMessage response, HttpRequestHeaders sentHeaders, CancellationToken cancellationToken)
    {
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new GitHubErrorResponse(response, sentHeaders.Authorization is not null, MessageOf(body));
    }

    /// <summary>
    /// The message for a rejected token or an exhausted rate limit, or <c>null</c> when
    /// the error is neither. Both messages are the same whatever the request was for.
    /// </summary>
    public string? DescribeCredentials(string tokenVariable)
    {
        if (StatusCode == HttpStatusCode.Unauthorized && WithToken)
        {
            return $"GitHub rejected the token in {tokenVariable} ({Status}): it is expired, revoked or mistyped.";
        }

        if (RateLimited)
        {
            string remedy = WithToken
                ? "Wait until it resets and run again."
                : $"A token in {tokenVariable} raises it; see the warning at the start of the run.";
            return $"GitHub's rate limit is spent ({Status}). {remedy}";
        }

        return null;
    }

    /// <summary>The line in an error message that says which token the request carried.</summary>
    public string DescribeToken(string tokenVariable)
        => WithToken
            ? $"The request carried the token from {tokenVariable}."
            : $"The request carried no token: {tokenVariable} is not set.";

    private static string? Header(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out IEnumerable<string>? values) ? values.FirstOrDefault() : null;

    private static string MessageOf(string body)
    {
        try
        {
            if (JsonSerializer.Deserialize(body, GitHubErrorJsonContext.Default.GitHubErrorDto)?.Message is { Length: > 0 } message)
            {
                return message;
            }
        }
        catch (JsonException)
        {
            // Not JSON, for example a proxy's HTML page. Fall back to the raw body.
        }

        return body.Length <= 200 ? body : body[..200] + "...";
    }
}

/// <summary>The body GitHub sends with an error status. Only its message is read.</summary>
internal sealed class GitHubErrorDto
{
    public string? Message { get; init; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GitHubErrorDto))]
internal sealed partial class GitHubErrorJsonContext : JsonSerializerContext;
