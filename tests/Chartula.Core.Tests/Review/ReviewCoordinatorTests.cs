using Chartula.Core.Llm;
using Chartula.Core.Review;

namespace Chartula.Core.Tests.Review;

public sealed class ReviewCoordinatorTests
{
    private static ReviewItem Item(string text = "Dark mode is here.", params string[] flags)
        => new(Audience.Customer, text, flags);

    private sealed class StubReviewer(ReviewDecision decision) : IReviewer
    {
        public int Calls { get; private set; }

        public Task<ReviewDecision> ReviewAsync(ReviewItem item, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(decision);
        }
    }

    [Fact]
    public async Task With_review_off_it_approves_as_is_without_consulting_the_reviewer()
    {
        StubReviewer reviewer = new(ReviewDecision.Edit("should not be used"));
        ReviewCoordinator coordinator = new(reviewer, new ReviewOptions(Enabled: false));

        ReviewDecision decision = await coordinator.ReviewAsync(Item("Original text."));

        Assert.Equal(ReviewOutcome.Approved, decision.Outcome);
        Assert.Equal("Original text.", decision.Text);
        Assert.Equal(0, reviewer.Calls); // opt-in: reviewer not consulted
    }

    [Fact]
    public async Task With_review_on_and_approval_it_keeps_the_original_text()
    {
        StubReviewer reviewer = new(ReviewDecision.Approve("Original text."));
        ReviewCoordinator coordinator = new(reviewer, new ReviewOptions(Enabled: true));

        ReviewDecision decision = await coordinator.ReviewAsync(Item("Original text.", "flag"));

        Assert.Equal(ReviewOutcome.Approved, decision.Outcome);
        Assert.Equal("Original text.", decision.Text);
        Assert.Equal(1, reviewer.Calls);
    }

    [Fact]
    public async Task With_review_on_and_an_edit_it_returns_the_edited_text()
    {
        StubReviewer reviewer = new(ReviewDecision.Edit("Corrected text."));
        ReviewCoordinator coordinator = new(reviewer, new ReviewOptions(Enabled: true));

        ReviewDecision decision = await coordinator.ReviewAsync(Item("Original text.", "flag"));

        Assert.Equal(ReviewOutcome.Edited, decision.Outcome);
        Assert.Equal("Corrected text.", decision.Text);
    }

    [Fact]
    public void Review_mode_is_off_by_default()
    {
        Assert.False(new ReviewOptions().Enabled);
    }
}
