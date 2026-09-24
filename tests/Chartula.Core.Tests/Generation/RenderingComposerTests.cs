using Chartula.Core.Facts;
using Chartula.Core.Generation;
using Chartula.Core.Llm;

namespace Chartula.Core.Tests.Generation;

public sealed class RenderingComposerTests
{
    private static RenderPlan Plan(params PlannedEntry[] entries)
        => new(new GroundedFacts([.. entries.Select(entry => $"[{entry.Id}] Fact {entry.Id}")]), entries);

    private static RenderedEntries Answer(params RenderedEntry[] entries) => new(entries);

    [Fact]
    public void Puts_each_group_under_one_heading_in_the_planned_order()
    {
        RenderPlan plan = Plan(
            new PlannedEntry(1, "Added", false, null),
            new PlannedEntry(2, "Added", false, null),
            new PlannedEntry(3, "Fixed", false, null));

        // The plan decides the order, not the order of the model's answer.
        string text = RenderingComposer.Compose(
            plan, Answer(new RenderedEntry(3, "Three"), new(1, "One"), new(2, "Two")), Audience.Product);

        Assert.Equal("### Added\n\n- One\n- Two\n\n### Fixed\n\n- Three", text);
    }

    [Fact]
    public void Ends_a_technical_entry_on_its_reference_and_marks_a_breaking_one()
    {
        RenderPlan plan = Plan(new PlannedEntry(1, "Changed", true, "([#5](https://example/pull/5))"));

        string text = RenderingComposer.Compose(plan, Answer(new RenderedEntry(1, "Drop the v1 format")), Audience.Technical);

        Assert.Equal("### Changed\n\n- **Breaking:** Drop the v1 format ([#5](https://example/pull/5))", text);
    }

    [Fact]
    public void Gives_a_customer_entry_its_label_and_a_breaking_one_the_marker_instead()
    {
        RenderPlan plan = Plan(
            new PlannedEntry(1, "What needs action", true, null),
            new PlannedEntry(2, "What's New", false, null),
            new PlannedEntry(3, "What's New", false, null));

        string text = RenderingComposer.Compose(
            plan,
            Answer(new RenderedEntry(1, "Old settings stop working.", "Settings"), new(2, "You can search.", "**Search:**"), new(3, "You can sort.")),
            Audience.Customer);

        Assert.Equal(
            "### What needs action\n\n- **Breaking:** Old settings stop working.\n\n"
            + "### What's New\n\n- **Search:** You can search.\n- You can sort.",
            text);
    }

    [Fact]
    public void Writes_what_the_model_wrote_as_one_line()
    {
        RenderPlan plan = Plan(new PlannedEntry(1, "Other", false, null));

        string text = RenderingComposer.Compose(plan, Answer(new RenderedEntry(1, "- First line\n  second   line ")), Audience.Product);

        Assert.Equal("### Other\n\n- First line second line", text);
    }

    [Fact]
    public void Accepts_an_answer_with_one_text_per_planned_entry_in_any_order()
    {
        RenderPlan plan = Plan(new PlannedEntry(1, "Other", false, null), new PlannedEntry(2, "Other", false, null));

        Assert.Null(RenderingComposer.FindMismatch(plan, Answer(new RenderedEntry(2, "Two"), new(1, "One"))));
    }

    [Fact]
    public void Names_a_fact_left_without_an_entry_including_one_left_blank()
    {
        RenderPlan plan = Plan(
            new PlannedEntry(1, "Other", false, null),
            new PlannedEntry(2, "Other", false, null),
            new PlannedEntry(3, "Other", false, null));

        // A fact without an entry would silently vanish from the rendering.
        string? mismatch = RenderingComposer.FindMismatch(plan, Answer(new RenderedEntry(1, "One"), new(3, "  ")));

        Assert.NotNull(mismatch);
        Assert.Contains("no entry for fact 2, 3", mismatch);
    }

    [Fact]
    public void Names_a_fact_answered_twice()
    {
        RenderPlan plan = Plan(new PlannedEntry(1, "Other", false, null));

        string? mismatch = RenderingComposer.FindMismatch(plan, Answer(new RenderedEntry(1, "One"), new(1, "Again")));

        Assert.Contains("more than one entry for fact 1", mismatch);
    }

    [Fact]
    public void Names_an_entry_for_a_fact_that_was_never_sent()
    {
        RenderPlan plan = Plan(new PlannedEntry(1, "Other", false, null));

        string? mismatch = RenderingComposer.FindMismatch(plan, Answer(new RenderedEntry(1, "One"), new(9, "Invented")));

        Assert.Contains("an entry for fact 9, which it was not sent", mismatch);
    }

    [Theory]
    [InlineData("Write release-<tag>.md", @"Write release-\<tag>.md")]
    [InlineData("Write `release-<tag>.md`", "Write `release-<tag>.md`")]
    [InlineData("Write ``a ` <b>`` and <c>", @"Write ``a ` <b>`` and \<c>")]
    [InlineData("An odd ` then <tag>", @"An odd ` then \<tag>")]
    [InlineData(@"Already \<tag>", @"Already \<tag>")]
    [InlineData("1 < 2 and <a> <b>", @"1 \< 2 and \<a> \<b>")]
    [InlineData("No brackets at all", "No brackets at all")]
    public void Escapes_an_angle_bracket_only_where_it_would_be_read_as_html(string written, string expected)
    {
        Assert.Equal(expected, RenderingComposer.EscapeAngleBrackets(written));
    }

    [Fact]
    public void Escapes_what_the_model_wrote_but_not_the_reference_the_plan_adds()
    {
        RenderPlan plan = Plan(new PlannedEntry(1, "Added", false, "([#5](https://example/pull/5))"));

        string text = RenderingComposer.Compose(
            plan, Answer(new RenderedEntry(1, "Write a release-<tag>.md page")), Audience.Technical);

        Assert.Equal(@"### Added

- Write a release-\<tag>.md page ([#5](https://example/pull/5))".ReplaceLineEndings("\n"), text);
    }
}
