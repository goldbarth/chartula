using Chartula.Core.Serialization;

namespace Chartula.Core.Tests.Serialization;

public sealed class ChangelogMarkdownComposerTests
{
    [Fact]
    public void Creates_a_titled_file_with_the_section_when_none_exists()
    {
        string result = ChangelogMarkdownComposer.Compose(null, "v1.0.0", null, "- Added search");

        Assert.Equal("# Changelog\n\n## 1.0.0\n\n- Added search\n", result);
    }

    [Fact]
    public void Opens_the_section_on_the_version_and_the_date_the_tag_was_made()
    {
        // Common Changelog's release heading: no "v", and the tag's own date.
        string result = ChangelogMarkdownComposer.Compose(null, "v1.0.0", new DateOnly(2026, 6, 14), "- Add search");

        Assert.Equal("# Changelog\n\n## 1.0.0 - 2026-06-14\n\n- Add search\n", result);
    }

    [Fact]
    public void Keeps_a_tag_that_is_not_a_version_as_it_is()
    {
        string result = ChangelogMarkdownComposer.Compose(null, "vnext", null, "- Add search");

        Assert.Contains("## vnext\n", result);
    }

    [Fact]
    public void Prepends_the_new_section_above_existing_history()
    {
        string existing = "# Changelog\n\n## 1.0.0\n\n- Added search\n";

        string result = ChangelogMarkdownComposer.Compose(existing, "v1.1.0", null, "- Fixed a crash");

        // Newest on top, older section preserved intact below.
        Assert.Equal(
            "# Changelog\n\n## 1.1.0\n\n- Fixed a crash\n\n## 1.0.0\n\n- Added search\n",
            result);
    }

    [Fact]
    public void Preserves_existing_sections_verbatim()
    {
        string existing = "# Changelog\n\n## v1.0.0 (2024-01-01)\n\n- Hand-written entry\n";

        string result = ChangelogMarkdownComposer.Compose(existing, "v1.1.0", null, "- New entry");

        Assert.Contains("## v1.0.0 (2024-01-01)\n\n- Hand-written entry", result);
    }

    [Fact]
    public void Running_twice_for_the_same_release_is_idempotent()
    {
        DateOnly date = new(2026, 6, 14);
        string once = ChangelogMarkdownComposer.Compose(null, "v1.0.0", date, "- Added search");
        string twice = ChangelogMarkdownComposer.Compose(once, "v1.0.0", date, "- Added search");

        Assert.Equal(once, twice);
    }

    [Fact]
    public void Re_running_a_release_replaces_its_section_in_place_without_reordering()
    {
        string state = ChangelogMarkdownComposer.Compose(null, "v1.0.0", null, "- Added search");
        state = ChangelogMarkdownComposer.Compose(state, "v1.1.0", null, "- Fixed a crash");

        // Re-run the older release with corrected content.
        string result = ChangelogMarkdownComposer.Compose(state, "v1.0.0", null, "- Added search and filters");

        // 1.1.0 stays on top; 1.0.0 is updated in place, not duplicated or moved.
        Assert.Equal(
            "# Changelog\n\n## 1.1.0\n\n- Fixed a crash\n\n## 1.0.0\n\n- Added search and filters\n",
            result);
    }

    [Fact]
    public void Re_running_a_release_written_under_the_old_heading_replaces_it()
    {
        // A file written before the heading dropped the "v" still holds the same
        // release; re-running it must not add a second section beside the first.
        string existing = "# Changelog\n\n## v1.1.0\n\n- Fixed a crash\n\n## v1.0.0\n\n- Added search\n";

        string result = ChangelogMarkdownComposer.Compose(existing, "v1.0.0", new DateOnly(2026, 6, 14), "- Add search");

        Assert.Equal(
            "# Changelog\n\n## v1.1.0\n\n- Fixed a crash\n\n## 1.0.0 - 2026-06-14\n\n- Add search\n",
            result);
    }

    [Fact]
    public void Handles_crlf_line_endings_in_the_existing_file()
    {
        string existing = "# Changelog\r\n\r\n## 1.0.0\r\n\r\n- Added search\r\n";

        string result = ChangelogMarkdownComposer.Compose(existing, "v1.1.0", null, "- Fixed a crash");

        Assert.Equal(
            "# Changelog\n\n## 1.1.0\n\n- Fixed a crash\n\n## 1.0.0\n\n- Added search\n",
            result);
    }

    [Fact]
    public void Rejects_a_blank_tag()
    {
        Assert.Throws<ArgumentException>(() => ChangelogMarkdownComposer.Compose(null, "  ", null, "- x"));
    }
}
