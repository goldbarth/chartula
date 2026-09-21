using System.Globalization;
using System.Text;
using Chartula.Cli.Configuration;
using Chartula.Core.Budget;
using Chartula.Core.Observability;

namespace Chartula.Cli.Commands;

/// <summary>
/// Says what a run can cost before it spends anything, and refuses one that could
/// cost more than <c>cost.ceiling</c>. The estimate goes to stderr, with the notices
/// about the run, so it stays out of a changelog redirected to a file.
/// </summary>
internal sealed class ConsoleRunBudget(LlmOptions llm, CostOptions cost, TextWriter error) : IRunBudget
{
    public void Approve(RunEstimate estimate)
    {
        ArgumentNullException.ThrowIfNull(estimate);

        ModelPrice? price = cost.Price ?? ModelPrice.For(llm);
        error.Write(Format(estimate, price));

        if (cost.Ceiling is not { } ceiling || estimate.Calls.Count == 0)
        {
            return;
        }

        if (price is null)
        {
            throw new InvalidOperationException(
                $"cost.ceiling is set, but no price is known for model '{llm.Model}', so the ceiling cannot be checked. " +
                "Set cost.inputPerMillionTokens and cost.outputPerMillionTokens, or remove cost.ceiling.");
        }

        decimal most = price.Cost(estimate.MaxInputTokens, estimate.MaxOutputTokens);
        if (most > ceiling)
        {
            throw new InvalidOperationException(
                $"This run could cost up to {Dollars(most)}, above cost.ceiling ({Dollars(ceiling)}), so no model was called. " +
                "Raise cost.ceiling, lower llm.maxOutputTokens, render fewer audiences with --audience, " +
                "or turn off faithfulness.thorough.");
        }
    }

    private string Format(RunEstimate estimate, ModelPrice? price)
    {
        StringBuilder text = new();
        if (estimate.Calls.Count == 0)
        {
            text.AppendLine("Estimate: no model call is needed.");
            return text.ToString();
        }

        text.AppendLine("Estimate before the first model call - an upper bound, not a forecast:");
        foreach (CallEstimate call in estimate.Calls)
        {
            text.AppendLine($"  {Name(call),-26} at most {Count(call.MaxInputTokens),9} in, {Count(call.MaxOutputTokens),9} out");
        }

        string calls = estimate.Calls.Count == 1 ? "1 call" : $"{estimate.Calls.Count} calls";
        text.Append($"  {"Total, " + calls,-26} at most {Count(estimate.MaxInputTokens),9} in, {Count(estimate.MaxOutputTokens),9} out");
        text.AppendLine(price is null
            ? $" - no price known for '{llm.Model}', so no cost"
            : $" = at most {Dollars(price.Cost(estimate.MaxInputTokens, estimate.MaxOutputTokens))}"
              + $" ({llm.Model} at {Dollars(price.InputPerMillionTokens)} / {Dollars(price.OutputPerMillionTokens)} per million tokens)");
        text.AppendLine(
            $"  Output is counted at llm.maxOutputTokens ({Count(estimate.Calls[0].MaxOutputTokens)} per call), the provider's hard cap; "
            + "a call typically uses a small part of it.");
        return text.ToString();
    }

    private static string Name(CallEstimate call)
    {
        string operation = call.Operation == LlmOperation.Rephrase ? "rephrase" : "thorough check";
        return $"{operation} {call.Audience.ToString().ToLowerInvariant()}";
    }

    private static string Count(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Dollars(decimal value) => "$" + value.ToString("0.00", CultureInfo.InvariantCulture);
}
