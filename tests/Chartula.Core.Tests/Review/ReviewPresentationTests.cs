using Chartula.Core.Llm;
using Chartula.Core.Review;

namespace Chartula.Core.Tests.Review;

public sealed class ReviewPresentationTests
{
    [Fact]
    public void Presents_the_text_and_highlights_the_flagged_passages()
    {
        ReviewItem item = new(Audience.Technical, "- Added dark mode",
            ["The number '3' is not supported by the facts.", "'TurboSync' is not supported by the facts."]);

        string presented = ReviewPresentation.Format(item);

        Assert.Contains("Technical", presented);
        Assert.Contains("- Added dark mode", presented);
        Assert.Contains("Flagged for review:", presented);
        Assert.Contains("! The number '3' is not supported by the facts.", presented);
        Assert.Contains("! 'TurboSync' is not supported by the facts.", presented);
    }

    [Fact]
    public void Omits_the_flag_section_when_there_are_no_flags()
    {
        ReviewItem item = new(Audience.Customer, "- Dark mode is here.", []);

        string presented = ReviewPresentation.Format(item);

        Assert.Contains("- Dark mode is here.", presented);
        Assert.DoesNotContain("Flagged for review:", presented);
    }
}
