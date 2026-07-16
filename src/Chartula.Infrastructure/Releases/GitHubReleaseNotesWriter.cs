using System.Net;
using System.Text;
using System.Text.Json;
using Chartula.Core.PullRequests;
using Chartula.Core.Releases;

namespace Chartula.Infrastructure.Releases;

/// <summary>
/// An <see cref="IReleaseNotesWriter"/> backed by the GitHub REST API over a plain
/// <see cref="HttpClient"/> (no SDK, so it stays AOT-friendly). It looks up the
/// release by tag: an existing release is updated in place (PATCH), a missing one
/// is created (POST). Because GitHub keys a release by its tag, re-running for the
/// same tag updates the same release rather than duplicating it.
/// </summary>
public sealed class GitHubReleaseNotesWriter(HttpClient httpClient) : IReleaseNotesWriter
{
    public async Task<string> WriteAsync(
        RepositoryCoordinates repository,
        string tag,
        string body,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(body);

        string basePath = $"repos/{repository.Owner}/{repository.Name}/releases";

        GitHubReleaseDto? existing = await GetByTagAsync(basePath, tag, cancellationToken);
        return existing is not null
            ? await UpdateAsync($"{basePath}/{existing.Id}", body, cancellationToken)
            : await CreateAsync(basePath, tag, body, cancellationToken);
    }

    private async Task<GitHubReleaseDto?> GetByTagAsync(string basePath, string tag, CancellationToken ct)
    {
        HttpResponseMessage response = await SendAsync(
            () => httpClient.GetAsync($"{basePath}/tags/{tag}", ct), tag);

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null; // no release for this tag yet
            }

            await EnsureSuccessAsync(response, tag, ct);
            return await ReadReleaseAsync(response, tag, ct);
        }
    }

    private async Task<string> UpdateAsync(string releasePath, string body, CancellationToken ct)
    {
        using StringContent content = JsonContent(GitHubReleaseJsonContext.Default.UpdateReleaseRequest,
            new UpdateReleaseRequest(body));
        HttpResponseMessage response = await SendAsync(() => httpClient.PatchAsync(releasePath, content, ct), releasePath);
        using (response)
        {
            await EnsureSuccessAsync(response, releasePath, ct);
            GitHubReleaseDto release = await ReadReleaseAsync(response, releasePath, ct);
            return release.HtmlUrl ?? string.Empty;
        }
    }

    private async Task<string> CreateAsync(string basePath, string tag, string body, CancellationToken ct)
    {
        using StringContent content = JsonContent(GitHubReleaseJsonContext.Default.CreateReleaseRequest,
            new CreateReleaseRequest(tag, body));
        HttpResponseMessage response = await SendAsync(() => httpClient.PostAsync(basePath, content, ct), tag);
        using (response)
        {
            await EnsureSuccessAsync(response, tag, ct);
            GitHubReleaseDto release = await ReadReleaseAsync(response, tag, ct);
            return release.HtmlUrl ?? string.Empty;
        }
    }

    private static async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send, string what)
    {
        try
        {
            return await send();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Could not reach the GitHub API for release '{what}': {ex.Message}", ex);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string what, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException(
            $"GitHub API returned {(int)response.StatusCode} {response.ReasonPhrase} for release '{what}'. {Truncate(body)}");
    }

    private static async Task<GitHubReleaseDto> ReadReleaseAsync(
        HttpResponseMessage response, string what, CancellationToken ct)
    {
        try
        {
            string json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize(json, GitHubReleaseJsonContext.Default.GitHubReleaseDto)
                   ?? throw new InvalidOperationException($"GitHub API returned an empty release for '{what}'.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"GitHub API returned an unexpected response for release '{what}': {ex.Message}", ex);
        }
    }

    private static StringContent JsonContent<T>(
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, T value)
        => new(JsonSerializer.Serialize(value, typeInfo), Encoding.UTF8, "application/json");

    private static string Truncate(string value) => value.Length <= 200 ? value : value[..200] + "...";
}
