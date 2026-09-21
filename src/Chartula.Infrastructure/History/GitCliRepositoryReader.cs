namespace Chartula.Infrastructure.History;

/// <summary>
/// Reads what a checkout already knows about the release a run is for, so the
/// command line does not have to repeat it. Both reads answer <c>null</c> rather
/// than throw when git has nothing to give: whether that is an error depends on
/// whether the flag it stands in for was passed.
/// </summary>
public sealed class GitCliRepositoryReader(GitExecutable git, string repositoryPath)
{
    /// <summary>
    /// The nearest tag reachable from <c>HEAD</c> - the release the checkout is at
    /// or has moved past. A tag on another branch is not a release of this one.
    /// </summary>
    public async Task<string?> ReadNearestTagAsync(CancellationToken cancellationToken = default)
        => Output(await GitCli.RunAsync(
            git, repositoryPath, ["describe", "--tags", "--abbrev=0", "HEAD"], cancellationToken));

    /// <summary>The URL of the named remote.</summary>
    public async Task<string?> ReadRemoteUrlAsync(string remote, CancellationToken cancellationToken = default)
        => Output(await GitCli.RunAsync(git, repositoryPath, ["remote", "get-url", remote], cancellationToken));

    private static string? Output(GitResult result)
        => result.ExitCode == 0 && result.StandardOutput.Trim() is { Length: > 0 } value ? value : null;
}
