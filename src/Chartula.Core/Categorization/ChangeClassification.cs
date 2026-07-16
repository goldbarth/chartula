namespace Chartula.Core.Categorization;

/// <summary>
/// The deterministic classification of a change: its category, plus whether it is
/// a breaking change (tracked separately so a breaking feature stays a feature
/// while still being flagged prominently).
/// </summary>
/// <param name="Category">The category decided from the change's convention.</param>
/// <param name="IsBreaking">
/// True when the change is marked breaking (a <c>!</c> before the colon, a
/// <c>breaking</c> type, or a <c>BREAKING CHANGE</c> note in the body).
/// </param>
public sealed record ChangeClassification(ChangeCategory Category, bool IsBreaking);
