namespace Chartula.Core.PullRequests;

/// <summary>
/// Identifies a repository on the hosting platform.
/// </summary>
/// <param name="Owner">The owning user or organization.</param>
/// <param name="Name">The repository name.</param>
public sealed record RepositoryCoordinates(string Owner, string Name);
