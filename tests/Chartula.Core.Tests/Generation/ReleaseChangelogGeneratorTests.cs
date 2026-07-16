using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Formatting;
using Chartula.Core.Generation;
using Chartula.Core.Llm;

namespace Chartula.Core.Tests.Generation;

public sealed class ReleaseChangelogGeneratorTests
{
    private static FactBase Sample(params ChangeFact[] changes)
        => new("v1.0.0", changes.Length == 0
            ? [new ChangeFact("feat: dark mode", 7, "https://example/pull/7",
                ChangeCategory.Feature, true, false, [], "Adds a theme.")]
            : changes);

    [Fact]
    public async Task Makes_one_call_through_the_provider_interface_and_returns_its_text()
    {
        FakeChangelogModel model = FakeChangelogModel.Returning("Dark mode is here.");
        // The generator depends only on the interface, never on a provider.
        IReleaseChangelogGenerator generator = new ReleaseChangelogGenerator(model, new ChangelogFormatter());

        ChangelogGenerationResult result = await generator.GenerateAsync(Sample(), Audience.Customer);

        Assert.True(result.IsSuccess);
        Assert.Equal("Dark mode is here.", result.Text);
        Assert.Equal(1, model.RephraseCallCount); // minimal: exactly one call per release
        Assert.Equal(Audience.Customer, model.LastRequest!.Audience);
    }

    [Fact]
    public async Task Normalizes_the_model_output_for_consistent_formatting()
    {
        FakeChangelogModel model = FakeChangelogModel.Returning("* Dark mode is here.  \n\n\n+ And search.");
        IReleaseChangelogGenerator generator = new ReleaseChangelogGenerator(model, new ChangelogFormatter());

        ChangelogGenerationResult result = await generator.GenerateAsync(Sample(), Audience.Customer);

        Assert.Equal("- Dark mode is here.\n\n- And search.", result.Text);
    }

    [Fact]
    public async Task Feeds_the_grounded_facts_from_the_fact_base()
    {
        FakeChangelogModel model = FakeChangelogModel.Returning("...");
        IReleaseChangelogGenerator generator = new ReleaseChangelogGenerator(model, new ChangelogFormatter());

        await generator.GenerateAsync(Sample(), Audience.Technical);

        string statement = Assert.Single(model.LastRequest!.Facts.Statements);
        Assert.Contains("Feature", statement);
        Assert.Contains("feat: dark mode", statement);
    }

    [Fact]
    public async Task Turns_a_provider_failure_into_a_failed_result_rather_than_throwing()
    {
        FakeChangelogModel model = FakeChangelogModel.Throwing(
            new InvalidOperationException("provider exploded"));
        IReleaseChangelogGenerator generator = new ReleaseChangelogGenerator(model, new ChangelogFormatter());

        ChangelogGenerationResult result = await generator.GenerateAsync(Sample(), Audience.Product);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Text);
        Assert.Contains("v1.0.0", result.Error);
        Assert.Contains("provider exploded", result.Error);
    }

    [Fact]
    public async Task Makes_no_call_for_an_empty_fact_base()
    {
        FakeChangelogModel model = FakeChangelogModel.Returning("should not be used");
        IReleaseChangelogGenerator generator = new ReleaseChangelogGenerator(model, new ChangelogFormatter());

        ChangelogGenerationResult result = await generator.GenerateAsync(
            new FactBase("v1.0.0", []), Audience.Customer);

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, result.Text);
        Assert.Equal(0, model.RephraseCallCount);
    }

    [Fact]
    public async Task Lets_cancellation_propagate()
    {
        FakeChangelogModel model = FakeChangelogModel.Returning("...");
        IReleaseChangelogGenerator generator = new ReleaseChangelogGenerator(model, new ChangelogFormatter());
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => generator.GenerateAsync(Sample(), Audience.Customer, cts.Token));
    }
}
