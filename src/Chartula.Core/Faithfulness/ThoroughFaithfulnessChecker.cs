using System.Text;
using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Faithfulness;

/// <summary>
/// Default <see cref="IThoroughFaithfulnessChecker"/>. When enabled, it turns the
/// fact base into grounded facts and runs a second LLM pass to flag claims the
/// facts do not support. When disabled, or when there is nothing to check, it
/// returns a faithful report without any LLM call. A call that fails leaves the text
/// unverified, which is what <see cref="FaithfulnessCheckStatus.NotEvaluated"/> says;
/// it does not take the rendering with it, which has already been paid for.
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

        // Disabled, or nothing to check: no LLM call.
        // Report it as skipped, not as clean, because nothing was verified.
        if (!_options.Enabled || string.IsNullOrWhiteSpace(output))
        {
            return FaithfulnessReport.Skipped;
        }

        GroundedFacts facts = ToGroundedFacts(factBase);
        try
        {
            return await _model.CheckFaithfulnessAsync(new FaithfulnessRequest(output, facts), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // These are the same failures a rendering reports as its error, for example a
            // rejected key, or a model the endpoint does not serve (faithfulness.model can
            // differ from the rendering's model).
            return FaithfulnessReport.NotEvaluated(ex.Message);
        }
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

            // Include the pull request reference. The composer adds it to the technical
            // rendering after the model has written. Without it here, the check reads
            // every reference as invented and flags every entry.
            if (change.Url is not null)
            {
                statement.Append(" (");
                if (change.Number is { } number)
                {
                    statement.Append("pull request #").Append(number).Append(", ");
                }

                statement.Append(change.Url).Append(')');
            }

            if (!string.IsNullOrEmpty(change.Description))
            {
                statement.Append(" - ").Append(change.Description);
            }

            statements.Add(statement.ToString());
        }

        return new GroundedFacts(statements);
    }
}
