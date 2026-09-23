namespace Chartula.Core.Review;

/// <summary>
/// Runs a rendering through review when review mode is on, and passes it through
/// unchanged when review mode is off. The text of the returned decision is written.
/// </summary>
public interface IReviewCoordinator
{
    Task<ReviewDecision> ReviewAsync(ReviewItem item, CancellationToken cancellationToken = default);
}
