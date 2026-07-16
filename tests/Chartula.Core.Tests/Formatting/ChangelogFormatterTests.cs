using Chartula.Core.Formatting;

namespace Chartula.Core.Tests.Formatting;

public sealed class ChangelogFormatterTests
{
    private readonly ChangelogFormatter _formatter = new();

    [Fact]
    public void Normalizes_mixed_bullet_markers_to_a_single_marker()
    {
        string result = _formatter.Format("* Added dark mode\n+ Fixed a crash\n• Improved speed");

        Assert.Equal("- Added dark mode\n- Fixed a crash\n- Improved speed", result);
    }

    [Fact]
    public void Normalizes_bullet_spacing()
    {
        string result = _formatter.Format("-   Added search\n*  Fixed a bug");

        Assert.Equal("- Added search\n- Fixed a bug", result);
    }

    [Fact]
    public void Normalizes_line_endings_and_trailing_whitespace()
    {
        string result = _formatter.Format("- Added search   \r\n- Fixed a bug\t");

        Assert.Equal("- Added search\n- Fixed a bug", result);
    }

    [Fact]
    public void Collapses_blank_line_runs_and_trims_leading_and_trailing_blanks()
    {
        string result = _formatter.Format("\n\n- Added search\n\n\n- Fixed a bug\n\n");

        Assert.Equal("- Added search\n\n- Fixed a bug", result);
    }

    [Fact]
    public void Leaves_non_bullet_lines_such_as_headings_intact()
    {
        string result = _formatter.Format("Features\n- Added search\n\nFixes\n- Fixed a bug");

        Assert.Equal("Features\n- Added search\n\nFixes\n- Fixed a bug", result);
    }

    [Fact]
    public void Is_idempotent()
    {
        string once = _formatter.Format("* Added dark mode\n\n\n+ Fixed a crash  ");

        Assert.Equal(once, _formatter.Format(once));
    }

    [Fact]
    public void Handles_empty_input()
    {
        Assert.Equal(string.Empty, _formatter.Format(string.Empty));
    }
}
