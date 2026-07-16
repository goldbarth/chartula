using System.Text.Json;
using Chartula.Core.History;
using Chartula.Core.PullRequests;

namespace Chartula.Infrastructure.PullRequests;

/// <summary>
/// An <see cref="IReleasePullRequestReader"/> backed by the GitHub REST API over a
/// plain <see cref="HttpClient"/>. Kept dependency-free (no SDK) to stay
/// AOT-friendly; the client's base address, auth, and headers are configured by
/// the composition root.
/// </summary>
public sealed class GitHubPullRequestReader(HttpClient httpClient) : IReleasePullRequestReader
{
    public async Task<IReadOnlyList<PullRequestInfo>> GetMergedPullRequestsAsync(
        RepositoryCoordinates repository,
        CommitRange range,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(range);

        List<PullRequestInfo> pullRequests = [];
        HashSet<int> seen = [];

        foreach (CommitInfo commit in range.Commits)
        {
            foreach (GitHubPullRequestDto dto in await GetPullsForCommitAsync(repository, commit.Sha, cancellationToken))
            {
                // Merged pull requests only, de-duplicated across commits.
                if (dto.MergedAt is null || !seen.Add(dto.Number))
                {
                    continue;
                }

                pullRequests.Add(new PullRequestInfo(
                    dto.Number,
                    dto.Title ?? string.Empty,
                    string.IsNullOrEmpty(dto.Body) ? null : dto.Body,
                    dto.Labels?
                        .Select(label => label.Name ?? string.Empty)
                        .Where(name => name.Length > 0)
                        .ToArray() ?? [],
                    dto.HtmlUrl ?? string.Empty));
            }
        }

        return pullRequests;
    }

    private async Task<GitHubPullRequestDto[]> GetPullsForCommitAsync(
        RepositoryCoordinates repository,
        string sha,
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
                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"GitHub API returned {(int)response.StatusCode} {response.ReasonPhrase} " +
                    $"for commit {sha}. {Truncate(body)}");
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

    private static string Truncate(string value)
        => value.Length <= 200 ? value : value[..200] + "...";
}
