using System.Text.Json.Serialization;

namespace Chartula.Infrastructure.PullRequests;

/// <summary>
/// Wire shapes for the GitHub "list pull requests associated with a commit"
/// endpoint. Only the fields Chartula needs are mapped.
/// </summary>
internal sealed class GitHubPullRequestDto
{
    public int Number { get; init; }

    public string? Title { get; init; }

    public string? Body { get; init; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; init; }

    [JsonPropertyName("merged_at")]
    public string? MergedAt { get; init; }

    public List<GitHubLabelDto>? Labels { get; init; }
}

internal sealed class GitHubLabelDto
{
    public string? Name { get; init; }
}

/// <summary>
/// Source-generated (reflection-free) serialization context, so PR parsing stays
/// AOT- and trim-safe.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GitHubPullRequestDto[]))]
internal sealed partial class GitHubJsonContext : JsonSerializerContext;
