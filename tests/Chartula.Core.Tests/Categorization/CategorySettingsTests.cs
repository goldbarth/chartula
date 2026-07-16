using Chartula.Core.Categorization;

namespace Chartula.Core.Tests.Categorization;

public sealed class CategorySettingsTests
{
    [Fact]
    public void Default_uses_enum_names_the_default_order_and_prominent_breaking()
    {
        CategorySettings settings = CategorySettings.Default;

        Assert.Equal("Feature", settings.DisplayName(ChangeCategory.Feature));
        Assert.True(settings.BreakingProminent);
        Assert.True(settings.RankOf(ChangeCategory.Feature) < settings.RankOf(ChangeCategory.Internal));
    }

    [Fact]
    public void From_parses_order_and_names_case_insensitively()
    {
        CategorySettings settings = CategorySettings.From(
            order: ["fix", "feature"],
            names: new Dictionary<string, string> { ["FIX"] = "Bug Fixes" },
            breakingProminent: false);

        Assert.True(settings.RankOf(ChangeCategory.Fix) < settings.RankOf(ChangeCategory.Feature));
        Assert.Equal("Bug Fixes", settings.DisplayName(ChangeCategory.Fix));
        Assert.False(settings.BreakingProminent);
    }

    [Fact]
    public void From_keeps_the_default_order_when_order_is_null()
    {
        CategorySettings settings = CategorySettings.From(order: null, names: null, breakingProminent: true);

        Assert.Equal(CategorySettings.DefaultOrder, settings.Order);
    }

    [Fact]
    public void An_unlisted_category_sorts_last()
    {
        CategorySettings settings = CategorySettings.From(
            order: ["Feature"], names: null, breakingProminent: true);

        Assert.True(settings.RankOf(ChangeCategory.Fix) > settings.RankOf(ChangeCategory.Feature));
    }

    [Theory]
    [InlineData("nonsense")]
    public void From_throws_a_clear_error_for_an_unknown_category(string bad)
    {
        InvalidOperationException fromOrder = Assert.Throws<InvalidOperationException>(
            () => CategorySettings.From([bad], null, true));
        Assert.Contains(bad, fromOrder.Message);

        InvalidOperationException fromNames = Assert.Throws<InvalidOperationException>(
            () => CategorySettings.From(null, new Dictionary<string, string> { [bad] = "x" }, true));
        Assert.Contains(bad, fromNames.Message);
    }
}
