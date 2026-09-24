namespace Chartula.Core.Review;

/// <summary>
/// Default <see cref="IReviewCoordinator"/>.
/// With review mode off, the default, it approves the text as-is without calling the
/// reviewer, so review is never forced on a release.
/// With review mode on, it hands the item to the reviewer and returns the reviewer's
/// decision to approve or edit.
/// </summary>
public sealed class ReviewCoordinator(IReviewer reviewer, ReviewOptions options) : IReviewCoordinator
{
    private readonly IReviewer _reviewer = reviewer ?? throw new ArgumentNullException(nameof(reviewer));
    private readonly ReviewOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public async Task<ReviewDecision> ReviewAsync(ReviewItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!_options.Enabled)
        {
            return ReviewDecision.Approve(item.Text);
        }

        return await _reviewer.ReviewAsync(item, cancellationToken);
    }
}
