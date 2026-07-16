using Chartula.Core.Curation;

namespace Chartula.Core.Categorization;

/// <summary>
/// Assigns a category (and breaking flag) to a change deterministically, with no
/// LLM involved. This runs before any generation so categories are established
/// facts the model cannot change.
/// </summary>
public interface IChangeCategorizer
{
    ChangeClassification Classify(ReleaseChange change);
}
