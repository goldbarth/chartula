using Chartula.Cli.Commands;
using Chartula.Cli.Configuration;
using Chartula.Core.Budget;
using Chartula.Core.Llm;
using Chartula.Core.Observability;

namespace Chartula.Cli.Tests.Commands;

public sealed class ConsoleRunBudgetTests
{
    // 100,000 in and 64,000 out: $0.20 + $0.64 = $0.84 on Sonnet 5's $2 / $10.
    private static readonly RunEstimate TwoCalls = new(
    [
        new CallEstimate(LlmOperation.Rephrase, Audience.Technical, 40_000, 32_000),
        new CallEstimate(LlmOperation.FaithfulnessCheck, Audience.Technical, 60_000, 32_000),
    ]);

    private static LlmOptions Llm(string model = "claude-sonnet-5", string provider = "anthropic", string? baseUrl = null)
        => new() { Provider = provider, Model = model, BaseUrl = baseUrl };

    private static (StringWriter Error, ConsoleRunBudget Budget) Budget(LlmOptions llm, CostOptions cost)
    {
        StringWriter error = new();
        return (error, new ConsoleRunBudget(llm, cost, error));
    }

    [Fact]
    public void Prints_every_call_the_total_and_the_cost_before_the_first_call()
    {
        (StringWriter error, ConsoleRunBudget budget) = Budget(Llm(), new CostOptions(null, null));

        budget.Approve(TwoCalls);

        string text = error.ToString();
        Assert.Contains("an upper bound", text);
        Assert.Contains("rephrase technical", text);
        Assert.Contains("thorough check technical", text);
        Assert.Contains("100,000 in", text);
        Assert.Contains("64,000 out", text);
        Assert.Contains("at most $0.84", text);
    }

    [Fact]
    public void An_unknown_model_is_estimated_in_tokens_and_given_no_cost()
    {
        (StringWriter error, ConsoleRunBudget budget) = Budget(
            Llm("qwen3:8b", "openai-compatible", "http://localhost:11434/v1"), new CostOptions(null, null));

        budget.Approve(TwoCalls);

        Assert.Contains("no price known for 'qwen3:8b'", error.ToString());
        Assert.DoesNotContain("$", error.ToString());
    }

    [Fact]
    public void A_proxy_in_front_of_anthropic_is_not_priced_as_anthropic()
    {
        (StringWriter error, ConsoleRunBudget budget) = Budget(
            Llm(baseUrl: "https://gateway.example.test"), new CostOptions(null, null));

        budget.Approve(TwoCalls);

        Assert.Contains("no price known", error.ToString());
    }

    [Fact]
    public void A_configured_price_overrides_the_table()
    {
        (StringWriter error, ConsoleRunBudget budget) = Budget(Llm(), new CostOptions(null, new ModelPrice(1m, 1m)));

        budget.Approve(TwoCalls);

        Assert.Contains("at most $0.16", error.ToString());
    }

    [Fact]
    public void A_run_above_the_ceiling_is_refused_naming_the_estimate_and_the_setting()
    {
        (_, ConsoleRunBudget budget) = Budget(Llm(), new CostOptions(0.50m, null));

        InvalidOperationException refusal = Assert.Throws<InvalidOperationException>(() => budget.Approve(TwoCalls));

        Assert.Contains("up to $0.84", refusal.Message);
        Assert.Contains("cost.ceiling ($0.50)", refusal.Message);
        Assert.Contains("no model was called", refusal.Message);
    }

    [Fact]
    public void A_run_at_or_under_the_ceiling_goes_ahead()
    {
        (_, ConsoleRunBudget budget) = Budget(Llm(), new CostOptions(0.84m, null));

        budget.Approve(TwoCalls);
    }

    [Fact]
    public void A_ceiling_without_a_price_cannot_pass_silently()
    {
        (_, ConsoleRunBudget budget) = Budget(Llm("some-new-model"), new CostOptions(100m, null));

        InvalidOperationException refusal = Assert.Throws<InvalidOperationException>(() => budget.Approve(TwoCalls));

        Assert.Contains("no price is known for model 'some-new-model'", refusal.Message);
        Assert.Contains("cost.inputPerMillionTokens", refusal.Message);
    }

    [Fact]
    public void A_run_that_needs_no_call_is_never_refused()
    {
        (StringWriter error, ConsoleRunBudget budget) = Budget(Llm("some-new-model"), new CostOptions(0m, null));

        budget.Approve(new RunEstimate([]));

        Assert.Contains("no model call is needed", error.ToString());
    }
}
