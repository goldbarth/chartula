using Chartula.Core.Curation;

namespace Chartula.Core.Categorization;

/// <summary>
/// Assigns a category and the breaking flag to a change deterministically, with no LLM.
/// It runs before generation, so categories are established facts the model cannot change.
/// </summary>
public interface IChangeCategorizer
{
    ChangeClassification Classify(ReleaseChange change);
}
