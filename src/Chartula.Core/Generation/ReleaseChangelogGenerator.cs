using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Formatting;
using Chartula.Core.Llm;

namespace Chartula.Core.Generation;

/// <summary>
/// Default <see cref="IReleaseChangelogGenerator"/>. It turns the fact base into
/// grounded fact statements (selected, ordered, and named per the category
/// settings) and makes exactly one <see cref="IChangelogModel"/> call per release,
/// then normalizes the output for consistent formatting. An empty fact base makes
/// no call at all. Provider failures are caught and returned as a failed result;
/// cancellation propagates.
/// <para>
/// The customer rendering carries a one-sentence description of the release, asked
/// for and returned in that same call, and lifted off the front of the text here -
/// see <see cref="ReleaseDescription"/>.
/// </para>
/// </summary>
public sealed class ReleaseChangelogGenerator(
    IChangelogModel model,
    IChangelogFormatter formatter,
    CategorySettings? categorySettings = null) : IReleaseChangelogGenerator
{
    private readonly IChangelogModel _model = model ?? throw new ArgumentNullException(nameof(model));
    private readonly IChangelogFormatter _formatter =
        formatter ?? throw new ArgumentNullException(nameof(formatter));
    private readonly CategorySettings _categorySettings = categorySettings ?? CategorySettings.Default;

    public async Task<ChangelogGenerationResult> GenerateAsync(
        FactBase factBase,
        Audience audience,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factBase);

        GroundedFacts facts = GroundedFactsFactory.Build(factBase, audience, _categorySettings);

        // Nothing to generate - skip the call entirely (keeps calls minimal).
        if (facts.Statements.Count == 0)
        {
            return ChangelogGenerationResult.Success(string.Empty);
        }

        try
        {
            string text = await _model.RephraseAsync(new RephraseRequest(facts, audience), cancellationToken);
            string formatted = _formatter.Format(text);

            // Only the customer page has a description, so only that rendering is
            // asked for one and only that one is read for it.
            if (audience != Audience.Customer)
            {
                return ChangelogGenerationResult.Success(formatted);
            }

            (string? description, string body) = ReleaseDescription.SplitOff(formatted);
            return ChangelogGenerationResult.Success(body, description);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ChangelogGenerationResult.Failure(
                $"Changelog generation for '{factBase.Tag}' failed: {ex.Message}");
        }
    }
}
