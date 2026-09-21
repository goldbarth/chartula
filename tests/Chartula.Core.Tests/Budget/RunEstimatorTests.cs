using System.Text;
using Chartula.Core.Budget;
using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Faithfulness;
using Chartula.Core.Formatting;
using Chartula.Core.Generation;
using Chartula.Core.Llm;
using Chartula.Core.Observability;
using Chartula.Core.Prompting;
using Chartula.Core.Tests.Fixtures;

namespace Chartula.Core.Tests.Budget;

public sealed class RunEstimatorTests
{
    private static RunEstimator Estimator(bool thorough = true, int maxOutputTokens = 32_000)
        => new(
            new ChangelogPromptBuilder(),
            new ChangelogFormatter(),
            new ChatModelOptions { MaxOutputTokens = maxOutputTokens },
            new ThoroughFaithfulnessOptions(thorough));

    [Fact]
    public void Every_rephrase_call_is_bounded_by_the_bytes_of_the_prompt_it_will_send()
    {
        FactBase factBase = FactBaseFixture.Load(FactBaseFixture.Typical);

        RunEstimate estimate = Estimator(thorough: false).Estimate(factBase, [Audience.Technical]);

        // The same builder, the same plan: the prompt the generator would send.
        RenderPlan plan = GroundedFactsFactory.Build(factBase, Audience.Technical, CategorySettings.Default, new HashSet<string>());
        ChangelogPrompt prompt = new ChangelogPromptBuilder().BuildRephrasePrompt(plan.Facts, Audience.Technical);
        long bytes = Encoding.UTF8.GetByteCount(prompt.System) + Encoding.UTF8.GetByteCount(prompt.User);

        CallEstimate call = Assert.Single(estimate.Calls);
        Assert.Equal(LlmOperation.Rephrase, call.Operation);
        Assert.True(call.MaxInputTokens >= bytes + RunEstimator.PerCallAllowance);
    }

    [Fact]
    public void Output_is_counted_at_the_ceiling_the_provider_enforces()
    {
        RunEstimate estimate = Estimator(maxOutputTokens: 8_000)
            .Estimate(FactBaseFixture.Load(FactBaseFixture.Typical), [Audience.Technical, Audience.Customer]);

        Assert.All(estimate.Calls, call => Assert.Equal(8_000, call.MaxOutputTokens));
        Assert.Equal(8_000 * estimate.Calls.Count, estimate.MaxOutputTokens);
    }

    [Fact]
    public void A_thorough_check_reads_back_at_most_the_output_before_it()
    {
        RunEstimate estimate = Estimator(maxOutputTokens: 8_000)
            .Estimate(FactBaseFixture.Load(FactBaseFixture.Typical), [Audience.Technical]);

        Assert.Equal([LlmOperation.Rephrase, LlmOperation.FaithfulnessCheck], estimate.Calls.Select(c => c.Operation));
        Assert.True(estimate.Calls[1].MaxInputTokens > 8_000 + RunEstimator.PerCallAllowance);
    }

    [Fact]
    public void With_the_thorough_check_off_only_rephrasing_is_counted()
    {
        RunEstimate estimate = Estimator(thorough: false).Estimate(FactBaseFixture.Load(FactBaseFixture.Typical), null);

        Assert.Equal(3, estimate.Calls.Count);
        Assert.All(estimate.Calls, call => Assert.Equal(LlmOperation.Rephrase, call.Operation));
    }

    [Fact]
    public void Only_the_audiences_asked_for_are_counted_in_the_order_they_render()
    {
        RunEstimate estimate = Estimator(thorough: false)
            .Estimate(FactBaseFixture.Load(FactBaseFixture.Typical), [Audience.Customer, Audience.Technical]);

        Assert.Equal([Audience.Technical, Audience.Customer], estimate.Calls.Select(c => c.Audience));
    }

    [Fact]
    public void A_release_with_nothing_to_render_needs_no_call()
        => Assert.Empty(Estimator().Estimate(FactBaseFixture.Load(FactBaseFixture.Empty), null).Calls);
}
