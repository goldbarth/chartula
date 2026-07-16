namespace Chartula.Core.Review;

/// <summary>
/// The single toggle for review mode. Off by default - review is opt-in and never
/// forced on a release.
/// </summary>
/// <param name="Enabled">Whether generated texts are presented for sign-off.</param>
public sealed record ReviewOptions(bool Enabled = false);
