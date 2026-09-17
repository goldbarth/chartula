using System.Text.Json.Serialization;

namespace Chartula.Infrastructure.Releases;

/// <summary>Wire shapes for the GitHub releases endpoints. Only the fields Chartula needs.</summary>
internal sealed class GitHubReleaseDto
{
    public long Id { get; init; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; init; }

    [JsonPropertyName("tag_name")]
    public string? TagName { get; init; }

    public bool Draft { get; init; }
}

/// <summary>Body for updating a release (PATCH).</summary>
internal sealed record UpdateReleaseRequest(
    [property: JsonPropertyName("body")] string Body);

/// <summary>
/// Body for creating a release (POST). Always a draft: generated notes are read by a
/// person before they go public, and publishing is a click on GitHub.
/// </summary>
internal sealed record CreateReleaseRequest(
    [property: JsonPropertyName("tag_name")] string TagName,
    [property: JsonPropertyName("body")] string Body)
{
    [JsonPropertyName("draft")]
    public bool Draft => true;
}

/// <summary>Source-generated (reflection-free) context, so release I/O stays AOT-safe.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GitHubReleaseDto))]
[JsonSerializable(typeof(List<GitHubReleaseDto>))]
[JsonSerializable(typeof(UpdateReleaseRequest))]
[JsonSerializable(typeof(CreateReleaseRequest))]
internal sealed partial class GitHubReleaseJsonContext : JsonSerializerContext;
