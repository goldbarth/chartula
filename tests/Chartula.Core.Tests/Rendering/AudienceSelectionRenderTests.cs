using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Generation;
using Chartula.Core.Llm;
using Chartula.Core.Rendering;

namespace Chartula.Core.Tests.Rendering;

/// <summary>
/// A run renders the audiences it asks for. Each one is a rephrasing call and a
/// faithfulness check of its own, so a run measuring one audience's wording pays
/// for one rather than three.
/// </summary>
public sealed class AudienceSelectionRenderTests
{
    private sealed class CountingGenerator : IReleaseChangelogGenerator
    {
        public List<Audience> Rendered { get; } = [];

        public Task<ChangelogGenerationResult> GenerateAsync(
            FactBase factBase, Audience audience, CancellationToken cancellationToken = default)
        {
            Rendered.Add(audience);
            return Task.FromResult(ChangelogGenerationResult.Success($"- {audience}"));
        }
    }

    private static FactBase Sample() => new(
        "v1.0.0",
        [new ChangeFact("feat: add search", 7, "https://example/pull/7", ChangeCategory.Feature,
            IsUserVisible: true, IsBreaking: false, [], "Adds search.")]);

    [Fact]
    public async Task Renders_every_audience_when_none_is_named()
    {
        CountingGenerator generator = new();

        await new ReleaseRenderer(generator).RenderAsync(Sample());

        Assert.Equal([Audience.Technical, Audience.Customer, Audience.Product], generator.Rendered);
    }

    [Fact]
    public async Task Renders_only_the_audience_asked_for()
    {
        CountingGenerator generator = new();

        var renderings = await new ReleaseRenderer(generator).RenderAsync(Sample(), [Audience.Customer]);

        Assert.Equal([Audience.Customer], generator.Rendered);
        Assert.Equal([Audience.Customer], renderings.Keys);
    }

    [Fact]
    public async Task Keeps_its_own_order_whatever_order_it_was_asked_in()
    {
        // Two runs asking for the same audiences make the same calls in the same
        // order, so a figure from one is comparable with a figure from the other.
        CountingGenerator generator = new();

        await new ReleaseRenderer(generator).RenderAsync(Sample(), [Audience.Product, Audience.Technical]);

        Assert.Equal([Audience.Technical, Audience.Product], generator.Rendered);
    }
}
