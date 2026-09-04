using Chartula.Core.Generation;

namespace Chartula.Core.Tests.Generation;

public sealed class ReleaseDescriptionTests
{
    [Fact]
    public void Lifts_the_labelled_first_line_off_the_body()
    {
        (string? description, string body) = ReleaseDescription.SplitOff(
            "Description: A release about faster runs.\n\n### What's New\n\n- **Search:** You can find things.");

        Assert.Equal("A release about faster runs.", description);
        Assert.Equal("### What's New\n\n- **Search:** You can find things.", body);
    }

    [Fact]
    public void Text_without_the_label_is_all_body()
    {
        const string text = "### What's New\n\n- **Search:** You can find things.";

        (string? description, string body) = ReleaseDescription.SplitOff(text);

        Assert.Null(description);
        Assert.Equal(text, body);
    }

    [Fact]
    public void A_bolded_label_is_still_the_answer_to_the_question_asked()
    {
        (string? description, string body) =
            ReleaseDescription.SplitOff("**Description:** A release about faster runs.\n\n- Something.");

        Assert.Equal("A release about faster runs.", description);
        Assert.Equal("- Something.", body);
    }

    [Fact]
    public void A_label_with_nothing_after_it_yields_no_description_and_is_not_body()
    {
        // The model was asked to leave the line out; an empty one must not become
        // an empty field or a stray line at the top of the page.
        (string? description, string body) = ReleaseDescription.SplitOff("Description:\n\n- Something.");

        Assert.Null(description);
        Assert.Equal("- Something.", body);
    }

    [Fact]
    public void Bold_inside_the_description_survives()
    {
        (string? description, _) =
            ReleaseDescription.SplitOff("Description: A release about **faster** runs.\n\n- Something.");

        Assert.Equal("A release about **faster** runs.", description);
    }

    [Fact]
    public void A_line_with_nothing_under_it_is_the_body_and_not_a_description()
    {
        // The run of 2026-09-04 wrote a customer rendering of zero characters and no
        // page, with no error anywhere: the whole answer had been lifted out as the
        // description. A description opens something, so one with nothing under it
        // was read wrong, and the text is the only thing the run has.
        const string text = "Description: All there is.";

        (string? description, string body) = ReleaseDescription.SplitOff(text);

        Assert.Null(description);
        Assert.Equal(text, body);
    }

    [Fact]
    public void A_line_followed_only_by_blanks_is_the_body_too()
    {
        const string text = "Description: All there is.\n\n   \n";

        (string? description, string body) = ReleaseDescription.SplitOff(text);

        Assert.Null(description);
        Assert.Equal(text, body);
    }

    [Fact]
    public void One_entry_under_the_line_is_enough_to_split()
    {
        // The guard above turns on the body being empty, not on how much of one
        // there is: a single entry still leaves a description to lift.
        (string? description, string body) =
            ReleaseDescription.SplitOff("Description: A release about faster runs.\n\n- Something.");

        Assert.Equal("A release about faster runs.", description);
        Assert.Equal("- Something.", body);
    }
}
