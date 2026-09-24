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
/// <para>
/// The model names the pull request each claim is about. Whether that number is a fact
/// of the release is decided here, against the fact base, not taken from the model.
/// </para>
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
        FaithfulnessReport report;
        try
        {
            report = await _model.CheckFaithfulnessAsync(new FaithfulnessRequest(output, facts), cancellationToken);
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

        HashSet<int> pullRequests = [.. factBase.Changes.Select(static change => change.Number).OfType<int>()];
        return report with
        {
            UnsupportedClaims = [.. report.UnsupportedClaims.Select(flag => Established(flag, pullRequests))],
        };
    }

    // A number the fact base does not have is no fact of this release, whatever the model
    // read it as: an issue a title mentions reads like a pull request. The flag keeps the
    // number in its text, so the reviewer still has the model's lead, but not as its fact.
    private static FaithfulnessFlag Established(FaithfulnessFlag flag, HashSet<int> pullRequests)
        => flag.PullRequest is not { } number || pullRequests.Contains(number)
            ? flag
            : new FaithfulnessFlag(
                $"{flag.Text} (The check named #{number}, which is not a pull request of this release.)");

    // The full fact base as grounded statements, so the check compares the output
    // against every established fact.
    private static GroundedFacts ToGroundedFacts(FactBase factBase)
    {
        List<string> statements = [];
        foreach (ChangeFact change in factBase.Changes)
        {
            StringBuilder statement = new();

            // Open on the pull request number, so the check names each claim's fact by it.
            // Its place is fixed because the title and description mention other numbers,
            // issues and earlier pull requests among them.
            // The number and the link are also the reference the composer adds to the
            // technical rendering after the model has written. Without them here, the
            // check reads every reference as invented and flags every entry.
            if (change.Number is { } number)
            {
                statement.Append("[#").Append(number).Append("] ");
            }

            statement.Append(change.Category);
            if (change.IsBreaking)
            {
                statement.Append(" (breaking)");
            }

            statement.Append(": ").Append(change.Title);
            if (change.Url is not null)
            {
                statement.Append(" (").Append(change.Url).Append(')');
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
