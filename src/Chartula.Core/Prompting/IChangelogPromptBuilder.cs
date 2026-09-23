using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Prompting;

/// <summary>
/// Builds the prompt for rephrasing a release's facts.
/// It passes in the fact base and restricts the model to rephrasing, so the output
/// stays trustworthy.
/// It never decides categories or flags: they arrive as established facts.
/// </summary>
public interface IChangelogPromptBuilder
{
    ChangelogPrompt BuildRephrasePrompt(GroundedFacts facts, Audience audience);

    /// <summary>
    /// Builds the prompt for the thorough faithfulness check: verify the generated
    /// output against the facts and flag any unsupported claim, including distortions
    /// of meaning.
    /// </summary>
    ChangelogPrompt BuildFaithfulnessPrompt(string output, GroundedFacts facts);
}
