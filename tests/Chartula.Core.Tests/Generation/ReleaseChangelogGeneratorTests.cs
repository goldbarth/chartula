using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Formatting;
using Chartula.Core.Generation;
using Chartula.Core.Labeling;
using Chartula.Core.Llm;

namespace Chartula.Core.Tests.Generation;

public sealed class ReleaseChangelogGeneratorTests
{
    private static ChangeFact Feature(int number, string title, IReadOnlyList<string>? labels = null)
        => new(title, number, $"https://example/pull/{number}",
            ChangeCategory.Feature, true, false, [], labels ?? [], "Adds a theme.");

    private static FactBase Sample(params ChangeFact[] changes)
        => new("v1.0.0", changes.Length == 0 ? [Feature(7, "feat: dark mode")] : changes);

    [Fact]
    public async Task Makes_one_call_through_the_provider_interface_and_composes_its_entries()
    {
        FakeChangelogModel model = FakeChangelogModel.Answering(
            request => Entries.Each(request.Facts, (_, _) => "Dark mode is here."));
        // The generator depends only on the interface, never on a provider.
        IReleaseChangelogGenerator generator = new ReleaseChangelogGenerator(model, new ChangelogFormatter());

        ChangelogGenerationResult result = await generator.GenerateAsync(Sample(), Audience.Customer);

        Assert.True(result.IsSuccess);
        Assert.Equal("### What's New\n\n- Dark mode is here.", result.Text);
        Assert.Equal(1, model.RephraseCallCount); // minimal: exactly one call per release
        Assert.Equal(Audience.Customer, model.LastRequest!.Audience);
    }

    [Fact]
    public async Task Feeds_the_planned_facts_to_the_model()
    {
        FakeChangelogModel model = FakeChangelogModel.Echoing();
        IReleaseChangelogGenerator generator = new ReleaseChangelogGenerator(model, new ChangelogFormatter());

        await generator.GenerateAsync(Sample(), Audience.Technical);

        string statement = Assert.Single(model.LastRequest!.Facts.Statements);
        Assert.StartsWith("[1] Feature", statement);
        Assert.Contains("feat: dark mode", statement);
    }

    [Fact]
    public async Task Ends_a_technical_entry_on_the_reference_the_model_never_saw()
    {
        FakeChangelogModel model = FakeChangelogModel.Answering(
            request => Entries.Each(request.Facts, (_, _) => "Add dark mode"));
        IReleaseChangelogGenerator generator = new ReleaseChangelogGenerator(model, new ChangelogFormatter());

        ChangelogGenerationResult result = await generator.GenerateAsync(Sample(), Audience.Technical);

        Assert.DoesNotContain("https://", model.LastRequest!.Facts.Statements[0]);
        Assert.Equal("### Added\n\n- Add dark mode ([#7](https://example/pull/7))", result.Text);
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
    public async Task Fails_the_audience_when_the_answer_leaves_a_fact_without_an_entry()
    {
        // Skipping the fact would silently drop a change from the rendering, and filling
        // it in would show the reader a raw title.
        FakeChangelogModel model = FakeChangelogModel.Answering(_ => new RenderedEntries([new RenderedEntry(1, "One")]));
        IReleaseChangelogGenerator generator = new ReleaseChangelogGenerator(model, new ChangelogFormatter());

        ChangelogGenerationResult result = await generator.GenerateAsync(
            Sample(Feature(7, "feat: dark mode"), Feature(8, "feat: search")), Audience.Product);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Text);
        Assert.Contains("no entry for fact 2", result.Error);
    }

    [Fact]
    public async Task Makes_no_call_for_an_empty_fact_base()
    {
        FakeChangelogModel model = FakeChangelogModel.Echoing();
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
        FakeChangelogModel model = FakeChangelogModel.Echoing();
        IReleaseChangelogGenerator generator = new ReleaseChangelogGenerator(model, new ChangelogFormatter());
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => generator.GenerateAsync(Sample(), Audience.Customer, cts.Token));
    }

    [Fact]
    public async Task The_customer_rendering_carries_the_description_written_in_the_same_call()
    {
        FakeChangelogModel model = FakeChangelogModel.Echoing(description: "  A release about\nfinding things. ");
        IReleaseChangelogGenerator generator = new ReleaseChangelogGenerator(model, new ChangelogFormatter());

        ChangelogGenerationResult result = await generator.GenerateAsync(Sample(), Audience.Customer);

        // One call, two fields: the description rephrases the same facts, so it costs
        // no extra call.
        Assert.Equal(1, model.RephraseCallCount);
        Assert.Equal("A release about finding things.", result.Description);
    }

    [Fact]
    public async Task An_empty_description_is_no_description()
    {
        FakeChangelogModel model = FakeChangelogModel.Echoing(description: "   ");
        IReleaseChangelogGenerator generator = new ReleaseChangelogGenerator(model, new ChangelogFormatter());

        ChangelogGenerationResult result = await generator.GenerateAsync(Sample(), Audience.Customer);

        Assert.Null(result.Description);
    }

    [Theory]
    [InlineData(Audience.Technical)]
    [InlineData(Audience.Product)]
    public async Task Only_the_customer_rendering_is_read_for_a_description(Audience audience)
    {
        FakeChangelogModel model = FakeChangelogModel.Echoing(description: "Not a field here.");
        IReleaseChangelogGenerator generator = new ReleaseChangelogGenerator(model, new ChangelogFormatter());

        ChangelogGenerationResult result = await generator.GenerateAsync(Sample(), audience);

        Assert.Null(result.Description);
    }

    [Fact]
    public async Task Puts_a_change_labelled_as_asking_something_at_the_top_of_the_customer_rendering()
    {
        FakeChangelogModel model = FakeChangelogModel.Answering(
            request => Entries.Each(request.Facts, (id, _) => $"Entry {id}."));
        IReleaseChangelogGenerator generator = new ReleaseChangelogGenerator(
            model, new ChangelogFormatter(), labelRules: new LabelRules(actionRequiredLabels: ["needs-migration"]));

        ChangelogGenerationResult result = await generator.GenerateAsync(
            Sample(Feature(7, "feat: dark mode"), Feature(8, "feat: move the label rules", ["needs-migration"])),
            Audience.Customer);

        Assert.Equal("### What needs action\n\n- Entry 1.\n\n### What's New\n\n- Entry 2.", result.Text);
        Assert.Contains("feat: move the label rules", model.LastRequest!.Facts.Statements[0]);
    }
}
