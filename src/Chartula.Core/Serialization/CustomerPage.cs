namespace Chartula.Core.Serialization;

/// <summary>
/// One release as a customer-facing page: the front matter a site needs, and the
/// rendered body below it. This is the published serialisation of the customer
/// rendering - one file per release - specified in <c>docs/output-format.md</c>
/// of goldbarth/chartula-evals.
/// </summary>
/// <param name="Tag">The release tag the page is for; the source of the title.</param>
/// <param name="PublishedAt">
/// The date the tag was created, or <c>null</c> when it could not be read.
/// </param>
/// <param name="Description">
/// One sentence on what the release is about, written by the model from the
/// release's facts, or <c>null</c> when it could not be written from them.
/// </param>
/// <param name="Tags">
/// Subject-matter tags, from the labels on the pull requests behind the release.
/// Empty until labels reach the fact base (issue #98), and an empty list means
/// the field is left out rather than emitted empty.
/// </param>
/// <param name="Body">The customer rendering, as it goes under the front matter.</param>
public sealed record CustomerPage(
    string Tag,
    DateOnly? PublishedAt,
    string? Description,
    IReadOnlyList<string> Tags,
    string Body);
