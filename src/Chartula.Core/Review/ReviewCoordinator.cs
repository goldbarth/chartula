namespace Chartula.Core.Review;

/// <summary>
/// Default <see cref="IReviewCoordinator"/>. When review mode is off (the
/// default), it approves the text as-is without consulting the reviewer, so
/// review is never forced on a release. When on, it hands the item to the
/// reviewer and returns their approve-or-edit decision.
/// </summary>
public sealed class ReviewCoordinator(IReviewer reviewer, ReviewOptions options) : IReviewCoordinator
{
    private readonly IReviewer _reviewer = reviewer ?? throw new ArgumentNullException(nameof(reviewer));
    private readonly ReviewOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public async Task<ReviewDecision> ReviewAsync(ReviewItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        // Opt-in: with review off, pass the text straight through.
        if (!_options.Enabled)
        {
            return ReviewDecision.Approve(item.Text);
        }

        return await _reviewer.ReviewAsync(item, cancellationToken);
    }
}
