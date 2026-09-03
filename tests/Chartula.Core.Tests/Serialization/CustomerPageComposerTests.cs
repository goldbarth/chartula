using Chartula.Core.Serialization;

namespace Chartula.Core.Tests.Serialization;

/// <summary>
/// The published serialisation of a customer rendering. These assert the shape of
/// the opening rather than that something was produced: the opening is what makes
/// the rendering a document a person can publish, and it is the part a reader of
/// the file never sees go wrong until a site refuses to parse it.
/// </summary>
public sealed class CustomerPageComposerTests
{
    // Every field present; a test that wants one absent takes it away with "with",
    // so "no value" is never confused with "the default value".
    private static CustomerPage Page(
        string tag = "v0.1.0",
        string? description = "One sentence on what this release is about.",
        IReadOnlyList<string>? tags = null,
        string body = "### What's New\n\n- **Search:** You can find things now.")
        => new(tag, new DateOnly(2026, 7, 17), description, tags ?? [], body);

    [Fact]
    public void Opens_with_front_matter_carrying_the_title_and_the_tag_date()
    {
        string page = CustomerPageComposer.Compose(Page());

        Assert.Equal(
            """
            ---
            title: Release 0.1.0
            description: One sentence on what this release is about.
            publishedAt: 2026-07-17
            ---

            ### What's New

            - **Search:** You can find things now.

            """.ReplaceLineEndings("\n"),
            page);
    }

    [Fact]
    public void The_title_drops_the_tag_prefix_but_keeps_a_tag_that_is_not_a_version()
    {
        Assert.Equal("Release 0.1.0", CustomerPageComposer.TitleFor("v0.1.0"));
        Assert.Equal("Release 0.1.0", CustomerPageComposer.TitleFor("0.1.0"));

        // A leading "v" is only a version prefix in front of a digit; "vega" is a name.
        Assert.Equal("Release vega", CustomerPageComposer.TitleFor("vega"));
    }

    [Fact]
    public void A_description_that_could_not_be_written_leaves_the_field_out()
    {
        string page = CustomerPageComposer.Compose(Page(description: null));

        Assert.DoesNotContain("description:", page, StringComparison.Ordinal);
        Assert.Contains("title: Release 0.1.0", page, StringComparison.Ordinal);
    }

    [Fact]
    public void A_blank_description_is_omitted_rather_than_emitted_empty()
    {
        // An empty field reads as a fact about the release - that it has nothing to
        // summarise - which is not what "nobody could write one" means.
        string page = CustomerPageComposer.Compose(Page(description: "   "));

        Assert.DoesNotContain("description:", page, StringComparison.Ordinal);
    }

    [Fact]
    public void A_tag_date_that_could_not_be_read_leaves_publishedAt_out()
    {
        string page = CustomerPageComposer.Compose(Page() with { PublishedAt = null });

        Assert.DoesNotContain("publishedAt:", page, StringComparison.Ordinal);
    }

    [Fact]
    public void Tags_are_omitted_while_there_are_none_and_listed_when_there_are()
    {
        Assert.DoesNotContain("tags:", CustomerPageComposer.Compose(Page()), StringComparison.Ordinal);

        string tagged = CustomerPageComposer.Compose(Page(tags: ["api", "release notes"]));
        Assert.Contains("tags:\n  - api\n  - release notes\n", tagged, StringComparison.Ordinal);
    }

    [Fact]
    public void The_fields_keep_the_order_the_format_specifies()
    {
        string page = CustomerPageComposer.Compose(Page(tags: ["api"]));

        int title = page.IndexOf("title:", StringComparison.Ordinal);
        int description = page.IndexOf("description:", StringComparison.Ordinal);
        int published = page.IndexOf("publishedAt:", StringComparison.Ordinal);
        int tags = page.IndexOf("tags:", StringComparison.Ordinal);

        Assert.True(title < description && description < published && published < tags);
    }

    [Fact]
    public void A_description_yaml_would_misread_is_quoted()
    {
        // A model writes a sentence, not a YAML scalar; "Preview: what a run would
        // produce" is a mapping to a parser and a sentence to a reader.
        string page = CustomerPageComposer.Compose(
            Page(description: "Preview: what a run would produce, without writing it."));

        Assert.Contains(
            "description: \"Preview: what a run would produce, without writing it.\"",
            page,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_quote_in_the_description_is_escaped_rather_than_closing_the_scalar()
    {
        string page = CustomerPageComposer.Compose(Page(description: "The \"thorough\" check: now optional."));

        Assert.Contains("description: \"The \\\"thorough\\\" check: now optional.\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public void The_body_follows_the_front_matter_after_one_blank_line()
    {
        string page = CustomerPageComposer.Compose(Page(body: "\r\n\r\n### Bug Fixes\r\n\r\n- Fixed.\r\n"));

        Assert.EndsWith("---\n\n### Bug Fixes\n\n- Fixed.\n", page, StringComparison.Ordinal);
    }

    [Fact]
    public void A_tag_is_required_because_the_title_has_no_other_source()
    {
        Assert.Throws<ArgumentException>(() => CustomerPageComposer.Compose(Page(tag: "  ")));
    }
}
