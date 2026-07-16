using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Prompting;

/// <summary>
/// Builds the prompt for rephrasing a release's facts. This is the prompt
/// architecture: it feeds the fact base in and constrains the model to rephrasing
/// only, so the output stays trustworthy. It never decides categories or flags -
/// those arrive as established facts.
/// </summary>
public interface IChangelogPromptBuilder
{
    ChangelogPrompt BuildRephrasePrompt(GroundedFacts facts, Audience audience);
}
