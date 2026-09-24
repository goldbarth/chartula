using Chartula.Cli.Commands;
using Chartula.Core.Llm;

namespace Chartula.Cli.Tests.Commands;

public sealed class AudienceSelectionTests
{
    private static IReadOnlyCollection<Audience>? Parse(params string[] args)
    {
        Assert.True(AudienceSelection.TryParse(args, out IReadOnlyCollection<Audience>? audiences, out string? error));
        Assert.Null(error);
        return audiences;
    }

    [Fact]
    public void No_option_renders_technical_and_customer_but_not_product()
    {
        Assert.Equal([Audience.Technical, Audience.Customer], Parse("generate", "--tag", "v1.0.0"));
    }

    [Fact]
    public void All_three_are_one_option_away()
    {
        Assert.Equal(
            [Audience.Technical, Audience.Customer, Audience.Product],
            Parse("generate", "--audience", "technical,customer,product"));
    }

    [Fact]
    public void One_audience_is_read_by_name()
    {
        Assert.Equal([Audience.Customer], Parse("generate", "--audience", "customer"));
    }

    [Fact]
    public void The_name_is_read_whatever_case_it_is_written_in()
    {
        Assert.Equal([Audience.Product], Parse("generate", "--audience", "PRODUCT"));
    }

    [Fact]
    public void Repeating_the_option_and_separating_with_commas_mean_the_same()
    {
        Assert.Equal(
            Parse("generate", "--audience", "technical", "--audience", "customer"),
            Parse("generate", "--audience", "technical,customer"));
    }

    [Fact]
    public void An_audience_named_twice_is_rendered_once()
    {
        Assert.Equal([Audience.Customer], Parse("generate", "--audience", "customer,customer"));
    }

    [Fact]
    public void An_unknown_name_is_an_error_that_names_the_three_that_exist()
    {
        // A misspelling that rendered nothing would look like a release with nothing to
        // say. That misreading must not be possible.
        Assert.False(AudienceSelection.TryParse(
            ["generate", "--audience", "custmer"], out IReadOnlyCollection<Audience>? audiences, out string? error));

        Assert.Null(audiences);
        Assert.NotNull(error);
        Assert.Contains("custmer", error);
        Assert.Contains("technical", error);
        Assert.Contains("customer", error);
        Assert.Contains("product", error);
    }
}
