using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Formatting;
using Chartula.Core.Generation;
using Chartula.Core.Llm;
using Chartula.Core.Rendering;
using Chartula.Core.Tests.Generation;

namespace Chartula.Core.Tests.Rendering;

public sealed class ReleaseRendererTests
{
    private static (ReleaseRenderer Renderer, RecordingChangelogModel Model) Build()
    {
        RecordingChangelogModel model = new();
        ReleaseRenderer renderer = new(new ReleaseChangelogGenerator(model, new ChangelogFormatter()));
        return (renderer, model);
    }

    private static FactBase Sample() => new("v1.0.0", [
        new ChangeFact("feat: dark mode", 7, "https://example/pull/7",
            ChangeCategory.Feature, IsUserVisible: true, IsBreaking: false, [], [], "Adds a theme."),
        new ChangeFact("refactor: reshape internals", 8, "https://example/pull/8",
            ChangeCategory.Refactor, IsUserVisible: false, IsBreaking: false, [], [], null),
    ]);

    [Fact]
    public async Task Renders_all_three_audiences_from_the_same_fact_base()
    {
        (ReleaseRenderer renderer, RecordingChangelogModel model) = Build();

        IReadOnlyDictionary<Audience, ChangelogGenerationResult> renderings =
            await renderer.RenderAsync(Sample());

        Assert.Equal(3, renderings.Count);
        Assert.True(renderings[Audience.Technical].IsSuccess);
        Assert.True(renderings[Audience.Customer].IsSuccess);
        Assert.True(renderings[Audience.Product].IsSuccess);
        Assert.Equal(3, model.RephraseCallCount); // one call per audience

        // Same source of truth: the user-visible feature appears in every audience.
        foreach (Audience audience in (Audience[])[Audience.Technical, Audience.Customer, Audience.Product])
        {
            Assert.Contains(model.StatementsFor(audience), s => s.Contains("feat: dark mode"));
        }
    }

    [Fact]
    public async Task Technical_keeps_links_and_leaves_out_a_refactor_nothing_outside_the_repository_meets()
    {
        (ReleaseRenderer renderer, RecordingChangelogModel model) = Build();

        IReadOnlyDictionary<Audience, ChangelogGenerationResult> renderings = await renderer.RenderAsync(Sample());

        // A1 of the technical rubric: a restructuring with identical behaviour does
        // not reach this reader either, so only the feature is handed over. Its link
        // is put on the entry by code rather than sent to the model.
        Assert.Single(model.StatementsFor(Audience.Technical));
        Assert.Contains("([#7](https://example/pull/7))", renderings[Audience.Technical].Text);
    }

    [Fact]
    public async Task Customer_omits_non_user_visible_changes_and_their_links()
    {
        (ReleaseRenderer renderer, RecordingChangelogModel model) = Build();

        await renderer.RenderAsync(Sample());

        IReadOnlyList<string> customer = model.StatementsFor(Audience.Customer);
        string only = Assert.Single(customer); // the internal refactor is dropped
        Assert.Contains("feat: dark mode", only);
        Assert.DoesNotContain("reshape internals", only);
        Assert.DoesNotContain("https://", only); // customer view carries no links
    }

    [Fact]
    public async Task Product_renders_from_the_same_base_as_the_others()
    {
        (ReleaseRenderer renderer, RecordingChangelogModel model) = Build();

        IReadOnlyDictionary<Audience, ChangelogGenerationResult> renderings = await renderer.RenderAsync(Sample());

        // Product sees what could move a decision, which an unlabelled refactor
        // does not, and every entry stands under the theme code gave it.
        Assert.Single(model.StatementsFor(Audience.Product));
        Assert.StartsWith("### Other", renderings[Audience.Product].Text);
    }

    [Fact]
    public async Task Surfaces_a_failed_audience_without_failing_the_others()
    {
        RecordingChangelogModel _ = new();
        // A generator whose model throws only for one audience.
        ReleaseRenderer renderer = new(new ReleaseChangelogGenerator(
            new SelectiveFailingModel(failFor: Audience.Customer), new ChangelogFormatter()));

        IReadOnlyDictionary<Audience, ChangelogGenerationResult> renderings =
            await renderer.RenderAsync(Sample());

        Assert.True(renderings[Audience.Technical].IsSuccess);
        Assert.False(renderings[Audience.Customer].IsSuccess);
        Assert.True(renderings[Audience.Product].IsSuccess);
    }

    private sealed class SelectiveFailingModel(Audience failFor) : IChangelogModel
    {
        public Task<RenderedEntries> RephraseAsync(RephraseRequest request, CancellationToken cancellationToken = default)
            => request.Audience == failFor
                ? throw new InvalidOperationException("boom")
                : Task.FromResult(Entries.Echo(request.Facts));

        public Task<FaithfulnessReport> CheckFaithfulnessAsync(
            FaithfulnessRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
