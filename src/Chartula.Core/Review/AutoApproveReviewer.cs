namespace Chartula.Core.Review;

/// <summary>
/// A non-interactive <see cref="IReviewer"/> that approves every rendering as-is.
/// The default reviewer until the interactive console reviewer ships with the CLI
/// command surface. With review mode off (the default) it is never consulted.
/// </summary>
public sealed class AutoApproveReviewer : IReviewer
{
    public Task<ReviewDecision> ReviewAsync(ReviewItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Task.FromResult(ReviewDecision.Approve(item.Text));
    }
}
