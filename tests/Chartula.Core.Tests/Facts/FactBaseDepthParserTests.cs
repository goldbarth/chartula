using Chartula.Core.Facts;

namespace Chartula.Core.Tests.Facts;

public sealed class FactBaseDepthParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Defaults_to_title_and_description_when_unset(string? value)
    {
        Assert.Equal(FactBaseDepth.TitleAndDescription, FactBaseDepthParser.Parse(value));
        Assert.Equal(FactBaseDepth.TitleAndDescription, FactBaseDepthParser.Default);
    }

    [Theory]
    [InlineData("title-only", FactBaseDepth.TitleOnly)]
    [InlineData("TitleOnly", FactBaseDepth.TitleOnly)]
    [InlineData("title", FactBaseDepth.TitleOnly)]
    [InlineData("title-and-description", FactBaseDepth.TitleAndDescription)]
    [InlineData("description", FactBaseDepth.TitleAndDescription)]
    [InlineData("title-description-and-issues", FactBaseDepth.TitleDescriptionAndIssues)]
    [InlineData("full", FactBaseDepth.TitleDescriptionAndIssues)]
    [InlineData("issues", FactBaseDepth.TitleDescriptionAndIssues)]
    public void Parses_names_and_aliases(string value, FactBaseDepth expected)
    {
        Assert.Equal(expected, FactBaseDepthParser.Parse(value));
    }

    [Fact]
    public void Throws_a_clear_error_for_an_unknown_depth()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => FactBaseDepthParser.Parse("bogus"));

        Assert.Contains("bogus", ex.Message);
    }
}
