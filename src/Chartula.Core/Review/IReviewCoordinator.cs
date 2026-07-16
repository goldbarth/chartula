namespace Chartula.Core.Review;

/// <summary>
/// Runs a rendering through review when review mode is on, and passes it straight
/// through when off. The returned decision's text is what gets written.
/// </summary>
public interface IReviewCoordinator
{
    Task<ReviewDecision> ReviewAsync(ReviewItem item, CancellationToken cancellationToken = default);
}
