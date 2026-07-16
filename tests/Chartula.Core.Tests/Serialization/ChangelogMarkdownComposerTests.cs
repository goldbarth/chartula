using Chartula.Core.Serialization;

namespace Chartula.Core.Tests.Serialization;

public sealed class ChangelogMarkdownComposerTests
{
    [Fact]
    public void Creates_a_titled_file_with_the_section_when_none_exists()
    {
        string result = ChangelogMarkdownComposer.Compose(null, "v1.0.0", "- Added search");

        Assert.Equal("# Changelog\n\n## v1.0.0\n\n- Added search\n", result);
    }

    [Fact]
    public void Prepends_the_new_section_above_existing_history()
    {
        string existing = "# Changelog\n\n## v1.0.0\n\n- Added search\n";

        string result = ChangelogMarkdownComposer.Compose(existing, "v1.1.0", "- Fixed a crash");

        // Newest on top, older section preserved intact below.
        Assert.Equal(
            "# Changelog\n\n## v1.1.0\n\n- Fixed a crash\n\n## v1.0.0\n\n- Added search\n",
            result);
    }

    [Fact]
    public void Preserves_existing_sections_verbatim()
    {
        string existing = "# Changelog\n\n## v1.0.0 (2024-01-01)\n\n- Hand-written entry\n";

        string result = ChangelogMarkdownComposer.Compose(existing, "v1.1.0", "- New entry");

        Assert.Contains("## v1.0.0 (2024-01-01)\n\n- Hand-written entry", result);
    }

    [Fact]
    public void Running_twice_for_the_same_release_is_idempotent()
    {
        string once = ChangelogMarkdownComposer.Compose(null, "v1.0.0", "- Added search");
        string twice = ChangelogMarkdownComposer.Compose(once, "v1.0.0", "- Added search");

        Assert.Equal(once, twice);
    }

    [Fact]
    public void Re_running_a_release_replaces_its_section_in_place_without_reordering()
    {
        string state = ChangelogMarkdownComposer.Compose(null, "v1.0.0", "- Added search");
        state = ChangelogMarkdownComposer.Compose(state, "v1.1.0", "- Fixed a crash");

        // Re-run the older release with corrected content.
        string result = ChangelogMarkdownComposer.Compose(state, "v1.0.0", "- Added search and filters");

        // v1.1.0 stays on top; v1.0.0 is updated in place, not duplicated or moved.
        Assert.Equal(
            "# Changelog\n\n## v1.1.0\n\n- Fixed a crash\n\n## v1.0.0\n\n- Added search and filters\n",
            result);
    }

    [Fact]
    public void Handles_crlf_line_endings_in_the_existing_file()
    {
        string existing = "# Changelog\r\n\r\n## v1.0.0\r\n\r\n- Added search\r\n";

        string result = ChangelogMarkdownComposer.Compose(existing, "v1.1.0", "- Fixed a crash");

        Assert.Equal(
            "# Changelog\n\n## v1.1.0\n\n- Fixed a crash\n\n## v1.0.0\n\n- Added search\n",
            result);
    }

    [Fact]
    public void Rejects_a_blank_tag()
    {
        Assert.Throws<ArgumentException>(() => ChangelogMarkdownComposer.Compose(null, "  ", "- x"));
    }
}
