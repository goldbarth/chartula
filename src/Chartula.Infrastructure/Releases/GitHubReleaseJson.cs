using System.Text.Json.Serialization;

namespace Chartula.Infrastructure.Releases;

/// <summary>Wire shapes for the GitHub releases endpoints. Only the fields Chartula needs.</summary>
internal sealed class GitHubReleaseDto
{
    public long Id { get; init; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; init; }
}

/// <summary>Body for updating a release (PATCH).</summary>
internal sealed record UpdateReleaseRequest(
    [property: JsonPropertyName("body")] string Body);

/// <summary>Body for creating a release (POST).</summary>
internal sealed record CreateReleaseRequest(
    [property: JsonPropertyName("tag_name")] string TagName,
    [property: JsonPropertyName("body")] string Body);

/// <summary>Source-generated (reflection-free) context, so release I/O stays AOT-safe.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GitHubReleaseDto))]
[JsonSerializable(typeof(UpdateReleaseRequest))]
[JsonSerializable(typeof(CreateReleaseRequest))]
internal sealed partial class GitHubReleaseJsonContext : JsonSerializerContext;
