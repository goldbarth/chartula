using Chartula.Cli.Terminal;

namespace Chartula.Cli.Tests.Terminal;

/// <summary>
/// #355: on a terminal a rendering is text to read, not the Markdown source of a file.
/// The five constructs a rendering is put together from are shown by weight, colour and
/// indent; everything else stays as written.
/// </summary>
public sealed class TerminalMarkdownTests
{
    private const string Reset = "\u001b[0m";
    private const string Bold = "\u001b[1m";
    private const string Cyan = "\u001b[36m";
    private const string BoldTeal = "\u001b[1m\u001b[36m";

    private static readonly TerminalProfile WithoutColor = new(Live: true, ColorDepth.None, Unicode: true);
    private static readonly TerminalProfile WithColor = new(Live: true, ColorDepth.Basic, Unicode: true);

    private static string Shown(string markdown, TerminalProfile? profile = null, int? columns = null)
        => string.Join('\n', TerminalMarkdown.Render(markdown, profile ?? WithoutColor, columns));

    [Fact]
    public void A_rendering_reads_as_headings_with_their_entries_below()
    {
        string shown = Shown("### Added\n\n- Add search.\n- Add export.\n\n### Fixed\n\n- Keep the filter on reload.\n");

        Assert.Equal(
            """
              Added
                • Add search.
                • Add export.

              Fixed
                • Keep the filter on reload.
            """.ReplaceLineEndings("\n"),
            shown);
    }

    [Fact]
    public void A_heading_a_label_and_the_breaking_marker_are_set_apart_by_weight()
    {
        string shown = Shown("### What needs action\n\n- **Breaking:** **Export:** It moved.", WithColor);

        Assert.Equal(
            $"  {Bold}What needs action{Reset}\n    • {BoldTeal}Breaking:{Reset} {Bold}Export:{Reset} It moved.",
            shown);
    }

    [Fact]
    public void Code_is_shown_by_colour_and_keeps_its_backticks_where_there_is_none()
    {
        const string Entry = "- Pass `--since <ref>` to `generate`.";

        Assert.Equal($"    • Pass {Cyan}--since <ref>{Reset} to {Cyan}generate{Reset}.", Shown(Entry, WithColor));
        Assert.Equal("    • Pass `--since <ref>` to `generate`.", Shown(Entry, WithoutColor));
    }

    // Variant (a) of the issue: the number says which pull request, and the URL is in CHANGELOG.md.
    [Fact]
    public void A_link_is_shown_as_its_text()
    {
        string shown = Shown("- Add search. ([#294](https://github.com/octo/repo/pull/294))");

        Assert.Equal("    • Add search. (#294)", shown);
    }

    [Theory]
    [InlineData("- A lone ` stays a backtick.", "    • A lone ` stays a backtick.")]
    [InlineData("- Two ** and no more.", "    • Two ** and no more.")]
    [InlineData("- An *aside* and a _name_ stay as written.", "    • An *aside* and a _name_ stay as written.")]
    public void What_is_not_one_of_the_five_constructs_stays_as_written(string entry, string expected)
    {
        Assert.Equal(expected, Shown(entry, WithColor));
    }

    [Fact]
    public void Nothing_inside_code_is_read_as_a_link_or_as_bold()
    {
        string shown = Shown("- Write `[#1](url)` or `**bold**`.", WithColor);

        Assert.Equal($"    • Write {Cyan}[#1](url){Reset} or {Cyan}**bold**{Reset}.", shown);
    }

    [Fact]
    public void A_description_is_a_paragraph_under_the_audience()
    {
        Assert.Equal("  A release about finding things.", Shown("A release about finding things."));
    }

    [Fact]
    public void Without_unicode_the_bullet_is_a_hyphen()
    {
        string shown = Shown("- Add search.", new TerminalProfile(Live: true, ColorDepth.None, Unicode: false));

        Assert.Equal("    - Add search.", shown);
    }

    [Fact]
    public void An_entry_longer_than_the_window_is_broken_between_words_with_a_hanging_indent()
    {
        string shown = Shown(
            "- **Run summaries:** Run summaries now open with whether generation completed or failed.", columns: 50);

        Assert.Equal(
            """
                • Run summaries: Run summaries now open with
                  whether generation completed or failed.
            """.ReplaceLineEndings("\n"),
            shown);
    }

    // Each row is painted on its own, so a colour never runs over a line break into the
    // indent of the next row, and every row can be read without the one before it.
    [Fact]
    public void A_style_that_spans_a_line_break_is_closed_and_opened_again()
    {
        string shown = Shown("- Pass `one two three four five six seven eight nine ten` here.", WithColor, columns: 40);

        string[] rows = shown.Split('\n');
        Assert.Equal($"    • Pass {Cyan}one two three four five six{Reset}", rows[0]);
        Assert.Equal($"      {Cyan}seven eight nine ten{Reset} here.", rows[1]);
    }

    [Fact]
    public void No_row_is_as_wide_as_the_window_at_any_width_with_room_for_text()
    {
        const string Rendering =
            "### What's New\n\n- **Published release protection:** `generate` now stops before model calls when the "
            + "tag already has a published release, and checks again before writing. ([#335](https://github.com/o/r/pull/335))";

        for (int columns = 30; columns <= 120; columns++)
        {
            Assert.All(
                TerminalMarkdown.Render(Rendering, WithoutColor, columns),
                row => Assert.True(row.Length < columns, $"{columns} columns: '{row}'"));
        }
    }

    // A word longer than the room, such as a URL someone wrote out, is not cut: it can still be copied.
    [Fact]
    public void A_word_longer_than_the_room_keeps_a_row_to_itself()
    {
        string shown = Shown("- See https://example.com/a/very/long/path/that/does/not/fit/in/the/window for more.", columns: 40);

        Assert.Equal(
            """
                • See
                  https://example.com/a/very/long/path/that/does/not/fit/in/the/window
                  for more.
            """.ReplaceLineEndings("\n"),
            shown);
    }

    // Too narrow to break an entry into rows worth reading, or no width known: the row is left to the terminal.
    [Theory]
    [InlineData(20)]
    [InlineData(null)]
    public void Without_room_or_without_a_width_a_row_is_left_whole(int? columns)
    {
        const string Entry = "- Run summaries now open with whether generation completed or failed.";

        Assert.Equal("    • " + Entry[2..], Shown(Entry, columns: columns));
    }
}
