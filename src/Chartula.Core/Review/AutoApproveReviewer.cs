namespace Chartula.Core.Review;

/// <summary>
/// A non-interactive <see cref="IReviewer"/> that approves every rendering as-is.
/// It is the default reviewer until the interactive console reviewer ships with the
/// CLI commands. With review mode off, the default, it is never called.
/// </summary>
public sealed class AutoApproveReviewer : IReviewer
{
    public Task<ReviewDecision> ReviewAsync(ReviewItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Task.FromResult(ReviewDecision.Approve(item.Text));
    }
}
