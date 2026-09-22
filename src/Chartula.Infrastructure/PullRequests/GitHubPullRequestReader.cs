using System.Net;
using System.Text.Json;
using Chartula.Core.History;
using Chartula.Core.PullRequests;
using Chartula.Infrastructure.GitHub;

namespace Chartula.Infrastructure.PullRequests;

/// <summary>
/// An <see cref="IReleasePullRequestReader"/> backed by the GitHub REST API over a
/// plain <see cref="HttpClient"/>. Kept dependency-free (no SDK) to stay
/// AOT-friendly; the client's base address, auth, and headers are configured by
/// the composition root.
/// </summary>
/// <param name="httpClient">The configured GitHub client.</param>
/// <param name="tokenVariable">
/// The environment variable the token is read from, named in an error so the
/// message points at what the caller can change.
/// </param>
public sealed class GitHubPullRequestReader(HttpClient httpClient, string tokenVariable) : IReleasePullRequestReader
{
    public async Task<IReadOnlyList<PullRequestInfo>> GetMergedPullRequestsAsync(
        RepositoryCoordinates repository,
        CommitRange range,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(range);

        // In the order first seen, with every commit of the range that belongs to each:
        // a revert that names commits is paired with pull requests through them.
        List<GitHubPullRequestDto> merged = [];
        Dictionary<int, List<string>> commitsByPull = [];

        bool first = true;
        foreach (CommitInfo commit in range.Commits)
        {
            GitHubPullRequestDto[] pulls = await GetPullsForCommitAsync(repository, commit.Sha, first, cancellationToken);
            first = false;

            foreach (GitHubPullRequestDto dto in pulls)
            {
                // Merged pull requests only, de-duplicated across commits.
                if (dto.MergedAt is null)
                {
                    continue;
                }

                if (!commitsByPull.TryGetValue(dto.Number, out List<string>? commits))
                {
                    commits = [];
                    commitsByPull[dto.Number] = commits;
                    merged.Add(dto);
                }

                commits.Add(commit.Sha);
            }
        }

        return [.. merged.Select(dto => new PullRequestInfo(
            dto.Number,
            dto.Title ?? string.Empty,
            string.IsNullOrEmpty(dto.Body) ? null : dto.Body,
            dto.Labels?
                .Select(label => label.Name ?? string.Empty)
                .Where(name => name.Length > 0)
                .ToArray() ?? [],
            dto.HtmlUrl ?? string.Empty)
        {
            CommitShas = commitsByPull[dto.Number],
        })];
    }

    private async Task<GitHubPullRequestDto[]> GetPullsForCommitAsync(
        RepositoryCoordinates repository,
        string sha,
        bool first,
        CancellationToken cancellationToken)
    {
        string path = $"repos/{repository.Owner}/{repository.Name}/commits/{sha}/pulls";

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(path, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Could not reach the GitHub API for commit {sha}: {ex.Message}", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                GitHubErrorResponse error = await GitHubErrorResponse.ReadAsync(
                    response, httpClient.DefaultRequestHeaders, cancellationToken);
                throw new InvalidOperationException(Describe(error, repository, sha, first));
            }

            try
            {
                string json = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Deserialize(json, GitHubJsonContext.Default.GitHubPullRequestDtoArray)
                       ?? [];
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"GitHub API returned an unexpected response for commit {sha}: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Names the cause the caller can act on. GitHub answers every request here with
    /// a status about one commit, but on the first request a 403 or 404 is about the
    /// repository or the credentials - nothing has been read from it yet - so the
    /// message says that instead of naming a commit.
    /// </summary>
    private string Describe(GitHubErrorResponse error, RepositoryCoordinates repository, string sha, bool first)
    {
        string repo = $"{repository.Owner}/{repository.Name}";

        if (error.DescribeCredentials(tokenVariable) is { } credentials)
        {
            return credentials;
        }

        // The endpoint reports a commit it does not know as 422, and so does a
        // repository that exists but is not the one the history was read from.
        if (error.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            return $"""
                Commit {sha} is not in {repo} on GitHub ({error.Status}).
                  Either the current directory is not a checkout of {repo} (check --repo),
                  or the commit has not been pushed.
                """;
        }

        if (first && error.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            string likely = error.WithToken
                ? $"  - the token cannot read {repo}, or lacks the Pull requests (read) permission"
                : $"  - {repo} is private, and reading it needs a token in {tokenVariable}";

            // GitHub names the permission a fine-grained token is missing.
            string permission = error.NeededPermission is { } needed
                ? $"\n  GitHub says the token needs: {needed}"
                : string.Empty;

            return $"""
                GitHub cannot read {repo} ({error.Status}).
                  {error.DescribeToken(tokenVariable)}{permission}
                  Likely causes:
                  - --repo is misspelled
                {likely}
                """;
        }

        return $"GitHub API returned {error.Status} for commit {sha}: {error.Message}";
    }
}
