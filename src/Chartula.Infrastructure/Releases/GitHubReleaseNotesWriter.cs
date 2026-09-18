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
/// is created as a draft (POST). Generated notes are a draft a person reads before
/// publishing, so nothing goes public on its own - and an update touches only the
/// body, so a release someone already published stays published.
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

        GitHubReleaseDto? existing = await GetByTagAsync(basePath, tag, cancellationToken)
                                     ?? await FindDraftByTagAsync(basePath, tag, cancellationToken);
        GitHubReleaseDto release = existing is not null
            ? await UpdateAsync($"{basePath}/{existing.Id}", tag, body, cancellationToken)
            : await CreateAsync(basePath, tag, body, cancellationToken);

        // A draft's link does not say it is one, and a reader who follows it expecting
        // a public release would otherwise not know there is a step left.
        string link = release.HtmlUrl ?? string.Empty;
        return release.Draft ? $"{link} (draft)" : link;
    }

    /// <summary>
    /// A draft for the tag from an earlier run. GitHub's lookup by tag answers only
    /// published releases, so without this every re-run would add another draft.
    /// The list includes drafts for a token with push access, which writing needs anyway.
    /// It lags a new release by a moment, so two runs for the same tag started within
    /// seconds of each other can still leave two drafts; a run takes longer than that.
    /// </summary>
    private async Task<GitHubReleaseDto?> FindDraftByTagAsync(string basePath, string tag, CancellationToken ct)
    {
        const int PageSize = 100;
        for (int page = 1; ; page++)
        {
            string path = $"{basePath}?per_page={PageSize}&page={page}";
            HttpResponseMessage response = await SendAsync(() => httpClient.GetAsync(path, ct), tag);
            List<GitHubReleaseDto> releases;
            using (response)
            {
                await EnsureSuccessAsync(response, tag, ct);
                releases = await ReadAsync(response, GitHubReleaseJsonContext.Default.ListGitHubReleaseDto, tag, ct);
            }

            if (releases.Find(release => release.Draft && release.TagName == tag) is { } draft)
            {
                return draft;
            }

            if (releases.Count < PageSize)
            {
                return null;
            }
        }
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

    private async Task<GitHubReleaseDto> UpdateAsync(string releasePath, string tag, string body, CancellationToken ct)
    {
        using StringContent content = JsonContent(GitHubReleaseJsonContext.Default.UpdateReleaseRequest,
            new UpdateReleaseRequest(tag, body));
        HttpResponseMessage response = await SendAsync(() => httpClient.PatchAsync(releasePath, content, ct), releasePath);
        using (response)
        {
            await EnsureSuccessAsync(response, releasePath, ct);
            return await ReadReleaseAsync(response, releasePath, ct);
        }
    }

    private async Task<GitHubReleaseDto> CreateAsync(string basePath, string tag, string body, CancellationToken ct)
    {
        using StringContent content = JsonContent(GitHubReleaseJsonContext.Default.CreateReleaseRequest,
            new CreateReleaseRequest(tag, body));
        HttpResponseMessage response = await SendAsync(() => httpClient.PostAsync(basePath, content, ct), tag);
        using (response)
        {
            await EnsureSuccessAsync(response, tag, ct);
            return await ReadReleaseAsync(response, tag, ct);
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

    private static Task<GitHubReleaseDto> ReadReleaseAsync(
        HttpResponseMessage response, string what, CancellationToken ct)
        => ReadAsync(response, GitHubReleaseJsonContext.Default.GitHubReleaseDto, what, ct);

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        string what,
        CancellationToken ct)
        where T : class
    {
        try
        {
            string json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize(json, typeInfo)
                   ?? throw new InvalidOperationException($"GitHub API returned an empty response for release '{what}'.");
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
