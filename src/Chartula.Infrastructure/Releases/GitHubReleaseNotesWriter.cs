using System.Net;
using System.Text;
using System.Text.Json;
using Chartula.Core.PullRequests;
using Chartula.Core.Releases;
using Chartula.Infrastructure.GitHub;

namespace Chartula.Infrastructure.Releases;

/// <summary>
/// An <see cref="IReleaseNotesWriter"/> backed by the GitHub REST API over a plain
/// <see cref="HttpClient"/>. It uses no SDK, so it stays AOT-friendly.
/// It looks up the release by tag:
/// <list type="bullet">
/// <item>An existing release is updated in place (PATCH). The update touches only the
/// body, so a release someone already published stays published.</item>
/// <item>A missing release is created as a draft (POST). A person reads the draft
/// before publishing, so nothing goes public on its own.</item>
/// </list>
/// </summary>
/// <param name="httpClient">The configured GitHub client.</param>
/// <param name="tokenVariable">
/// The environment variable the token is read from. Errors name it, so the message
/// points at what the caller can change.
/// </param>
public sealed class GitHubReleaseNotesWriter(HttpClient httpClient, string tokenVariable) : IReleaseNotesWriter
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

        Target target = new(repository, tag);
        GitHubReleaseDto? existing = await GetByTagAsync(basePath, target, cancellationToken)
                                     ?? await FindDraftByTagAsync(basePath, target, cancellationToken);
        GitHubReleaseDto release = existing is not null
            ? await UpdateAsync($"{basePath}/{existing.Id}", target, body, cancellationToken)
            : await CreateAsync(basePath, target, body, cancellationToken);

        // Mark a draft's link: the URL does not show it is a draft, and a reader who
        // expects a public release would not know a step is left.
        string link = release.HtmlUrl ?? string.Empty;
        return release.Draft ? $"{link} (draft)" : link;
    }

    /// <summary>
    /// Finds a draft for the tag from an earlier run.
    /// GitHub's lookup by tag finds only published releases, so without this every
    /// re-run would add another draft.
    /// The release list includes drafts for a token with push access, which writing
    /// needs anyway.
    /// The list shows a new release only after a moment. Two runs for the same tag
    /// started within seconds can still leave two drafts, but a run takes longer than that.
    /// </summary>
    private async Task<GitHubReleaseDto?> FindDraftByTagAsync(string basePath, Target target, CancellationToken ct)
    {
        const int PageSize = 100;
        for (int page = 1; ; page++)
        {
            string path = $"{basePath}?per_page={PageSize}&page={page}";
            HttpResponseMessage response = await SendAsync(() => httpClient.GetAsync(path, ct), target.Tag);
            List<GitHubReleaseDto> releases;
            using (response)
            {
                await EnsureSuccessAsync(response, target, ct);
                releases = await ReadAsync(response, GitHubReleaseJsonContext.Default.ListGitHubReleaseDto, target.Tag, ct);
            }

            if (releases.Find(release => release.Draft && release.TagName == target.Tag) is { } draft)
            {
                return draft;
            }

            if (releases.Count < PageSize)
            {
                return null;
            }
        }
    }

    private async Task<GitHubReleaseDto?> GetByTagAsync(string basePath, Target target, CancellationToken ct)
    {
        HttpResponseMessage response = await SendAsync(
            () => httpClient.GetAsync($"{basePath}/tags/{target.Tag}", ct), target.Tag);

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null; // no release for this tag yet
            }

            await EnsureSuccessAsync(response, target, ct);
            return await ReadReleaseAsync(response, target.Tag, ct);
        }
    }

    private async Task<GitHubReleaseDto> UpdateAsync(string releasePath, Target target, string body, CancellationToken ct)
    {
        using StringContent content = JsonContent(GitHubReleaseJsonContext.Default.UpdateReleaseRequest,
            new UpdateReleaseRequest(target.Tag, body));
        HttpResponseMessage response = await SendAsync(() => httpClient.PatchAsync(releasePath, content, ct), target.Tag);
        using (response)
        {
            await EnsureSuccessAsync(response, target, ct);
            return await ReadReleaseAsync(response, target.Tag, ct);
        }
    }

    private async Task<GitHubReleaseDto> CreateAsync(string basePath, Target target, string body, CancellationToken ct)
    {
        using StringContent content = JsonContent(GitHubReleaseJsonContext.Default.CreateReleaseRequest,
            new CreateReleaseRequest(target.Tag, body));
        HttpResponseMessage response = await SendAsync(() => httpClient.PostAsync(basePath, content, ct), target.Tag);
        using (response)
        {
            await EnsureSuccessAsync(response, target, ct);
            return await ReadReleaseAsync(response, target.Tag, ct);
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

    private async Task EnsureSuccessAsync(HttpResponseMessage response, Target target, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        GitHubErrorResponse error = await GitHubErrorResponse.ReadAsync(response, httpClient.DefaultRequestHeaders, ct);
        throw new InvalidOperationException(Describe(error, target));
    }

    /// <summary>
    /// Names a cause the caller can act on.
    /// The pull requests were read a moment earlier, so a refusal here is about writing.
    /// The likeliest cause is the read-only token the docs recommend until a run publishes.
    /// </summary>
    private string Describe(GitHubErrorResponse error, Target target)
    {
        string repo = $"{target.Repository.Owner}/{target.Repository.Name}";

        if (error.DescribeCredentials(tokenVariable) is { } credentials)
        {
            return credentials;
        }

        if (error.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            // GitHub names the permission a fine-grained token is missing.
            string permission = error.NeededPermission is { } needed
                ? $"\n  GitHub says the token needs: {needed}"
                : string.Empty;

            return $"""
                GitHub refused to publish the release notes for {target.Tag} to {repo} ({error.Status}).
                  {error.DescribeToken(tokenVariable)}{permission}
                  Publishing needs a token with Contents read and write on {repo}.
                """;
        }

        return $"GitHub API returned {error.Status} for release '{target.Tag}': {error.Message}";
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

    /// <summary>The release a run writes to, named in its errors.</summary>
    private sealed record Target(RepositoryCoordinates Repository, string Tag);
}
