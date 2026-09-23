namespace Chartula.Core.Categorization;

/// <summary>
/// The deterministic classification of a change: its category and whether it is breaking.
/// The breaking flag is separate from the category, so a breaking feature stays a
/// feature and is still flagged prominently.
/// </summary>
/// <param name="Category">The category decided from the change's convention.</param>
/// <param name="IsBreaking">
/// True when the change is marked breaking (a <c>!</c> before the colon, a
/// <c>breaking</c> type, or a <c>BREAKING CHANGE</c> note in the body).
/// </param>
public sealed record ChangeClassification(ChangeCategory Category, bool IsBreaking);
