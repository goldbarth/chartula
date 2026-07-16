using System.Text;
using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Faithfulness;

/// <summary>
/// Default <see cref="IThoroughFaithfulnessChecker"/>. When enabled, it turns the
/// fact base into grounded facts and runs a second LLM pass to flag claims the
/// facts do not support. When disabled, or when there is nothing to check, it
/// returns a faithful report without any LLM call.
/// </summary>
public sealed class ThoroughFaithfulnessChecker(
    IChangelogModel model,
    ThoroughFaithfulnessOptions options) : IThoroughFaithfulnessChecker
{
    private readonly IChangelogModel _model = model ?? throw new ArgumentNullException(nameof(model));
    private readonly ThoroughFaithfulnessOptions _options =
        options ?? throw new ArgumentNullException(nameof(options));

    public async Task<FaithfulnessReport> CheckAsync(
        string output,
        FactBase factBase,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(factBase);

        // Toggle off, or nothing to check: no second pass, no LLM call.
        if (!_options.Enabled || string.IsNullOrWhiteSpace(output))
        {
            return new FaithfulnessReport(IsFaithful: true, UnsupportedClaims: []);
        }

        GroundedFacts facts = ToGroundedFacts(factBase);
        return await _model.CheckFaithfulnessAsync(new FaithfulnessRequest(output, facts), cancellationToken);
    }

    // The full fact base as grounded statements, so the check compares the output
    // against every established fact.
    private static GroundedFacts ToGroundedFacts(FactBase factBase)
    {
        List<string> statements = [];
        foreach (ChangeFact change in factBase.Changes)
        {
            StringBuilder statement = new();
            statement.Append(change.Category);
            if (change.IsBreaking)
            {
                statement.Append(" (breaking)");
            }

            statement.Append(": ").Append(change.Title);
            if (!string.IsNullOrEmpty(change.Description))
            {
                statement.Append(" - ").Append(change.Description);
            }

            statements.Add(statement.ToString());
        }

        return new GroundedFacts(statements);
    }
}
