using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Generation;
using Chartula.Core.Llm;
using Chartula.Core.Rendering;

namespace Chartula.Core.Tests.Rendering;

public sealed class ReleaseRendererTests
{
    private static (ReleaseRenderer Renderer, RecordingChangelogModel Model) Build()
    {
        RecordingChangelogModel model = new();
        ReleaseRenderer renderer = new(new ReleaseChangelogGenerator(model));
        return (renderer, model);
    }

    private static FactBase Sample() => new("v1.0.0", [
        new ChangeFact("feat: dark mode", 7, "https://example/pull/7",
            ChangeCategory.Feature, IsUserVisible: true, IsBreaking: false, [], "Adds a theme."),
        new ChangeFact("refactor: reshape internals", 8, "https://example/pull/8",
            ChangeCategory.Refactor, IsUserVisible: false, IsBreaking: false, [], null),
    ]);

    [Fact]
    public async Task Renders_all_three_audiences_from_the_same_fact_base()
    {
        (ReleaseRenderer renderer, RecordingChangelogModel model) = Build();

        IReadOnlyDictionary<Audience, ChangelogGenerationResult> renderings =
            await renderer.RenderAllAsync(Sample());

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
    public async Task Technical_keeps_links_and_the_full_change_set()
    {
        (ReleaseRenderer renderer, RecordingChangelogModel model) = Build();

        await renderer.RenderAllAsync(Sample());

        IReadOnlyList<string> technical = model.StatementsFor(Audience.Technical);
        Assert.Equal(2, technical.Count); // includes the non-user-visible refactor
        Assert.Contains(technical, s => s.Contains("https://example/pull/7"));
    }

    [Fact]
    public async Task Customer_omits_non_user_visible_changes_and_their_links()
    {
        (ReleaseRenderer renderer, RecordingChangelogModel model) = Build();

        await renderer.RenderAllAsync(Sample());

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

        await renderer.RenderAllAsync(Sample());

        // Product sees the full set (grouping by theme is a prompt instruction).
        Assert.Equal(2, model.StatementsFor(Audience.Product).Count);
    }

    [Fact]
    public async Task Surfaces_a_failed_audience_without_failing_the_others()
    {
        RecordingChangelogModel _ = new();
        // A generator whose model throws only for one audience.
        ReleaseRenderer renderer = new(new ReleaseChangelogGenerator(
            new SelectiveFailingModel(failFor: Audience.Customer)));

        IReadOnlyDictionary<Audience, ChangelogGenerationResult> renderings =
            await renderer.RenderAllAsync(Sample());

        Assert.True(renderings[Audience.Technical].IsSuccess);
        Assert.False(renderings[Audience.Customer].IsSuccess);
        Assert.True(renderings[Audience.Product].IsSuccess);
    }

    private sealed class SelectiveFailingModel(Audience failFor) : IChangelogModel
    {
        public Task<string> RephraseAsync(RephraseRequest request, CancellationToken cancellationToken = default)
            => request.Audience == failFor
                ? throw new InvalidOperationException("boom")
                : Task.FromResult("ok");

        public Task<FaithfulnessReport> CheckFaithfulnessAsync(
            FaithfulnessRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
