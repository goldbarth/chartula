namespace Chartula.Core.Review;

/// <summary>
/// Presents a rendering to a human (or an automated stand-in) and returns their
/// decision to approve or edit. The interactive implementation lives with the CLI
/// command surface; the domain depends only on this seam.
/// </summary>
public interface IReviewer
{
    Task<ReviewDecision> ReviewAsync(ReviewItem item, CancellationToken cancellationToken = default);
}
