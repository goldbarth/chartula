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
            return ChangelogGenerationResult.Success(_formatter.Format(text));
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
