using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Chartula.Cli.Configuration;

/// <summary>
/// What a run may spend, and what a token costs when the price table does not
/// know the model. All three are optional: without a ceiling a run is estimated
/// but never refused.
/// </summary>
/// <param name="Ceiling">The most a run may cost, in US dollars, or <c>null</c> for no limit.</param>
/// <param name="Price">A configured price, which overrides the table, or <c>null</c>.</param>
internal sealed record CostOptions(decimal? Ceiling, ModelPrice? Price)
{
    /// <summary>Configuration section these options bind to.</summary>
    public const string SectionName = "Chartula:Cost";

    /// <summary>Reads the section, refusing a value that is not a non-negative number.</summary>
    public static CostOptions Read(IConfiguration configuration)
    {
        decimal? ceiling = ReadAmount(configuration, "Ceiling", "cost.ceiling");
        decimal? input = ReadAmount(configuration, "InputPerMillionTokens", "cost.inputPerMillionTokens");
        decimal? output = ReadAmount(configuration, "OutputPerMillionTokens", "cost.outputPerMillionTokens");

        // Half a price is no price: a cost from input alone would understate the run.
        if (input.HasValue != output.HasValue)
        {
            throw new InvalidOperationException(
                "cost.inputPerMillionTokens and cost.outputPerMillionTokens go together; set both or neither.");
        }

        return new CostOptions(ceiling, input is { } i && output is { } o ? new ModelPrice(i, o) : null);
    }

    private static decimal? ReadAmount(IConfiguration configuration, string key, string name)
    {
        string? raw = configuration[$"{SectionName}:{key}"];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value) || value < 0)
        {
            throw new InvalidOperationException($"Invalid {name} '{raw}'. Expected a number of US dollars, 0 or more.");
        }

        return value;
    }
}
