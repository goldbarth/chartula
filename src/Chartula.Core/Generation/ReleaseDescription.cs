namespace Chartula.Core.Generation;

/// <summary>
/// The one-sentence summary that opens a published customer page, and the one
/// place that knows how it travels out of the model.
/// <para>
/// The description is written in the same call as the audience text - it is a
/// rephrasing of facts already in front of the model, so a second call would buy
/// nothing - and arrives as a labelled first line. <see cref="Label"/> is the
/// label the prompt asks for and the label read back here, so the two cannot
/// drift apart.
/// </para>
/// </summary>
public static class ReleaseDescription
{
    /// <summary>The line label the model is asked to use, and the one read back.</summary>
    public const string Label = "Description:";

    /// <summary>
    /// Lifts the description off the front of <paramref name="text"/>, returning it
    /// and the body that follows. Without a labelled first line the whole text is
    /// the body and the description is <c>null</c> - a model that could not write
    /// one from the facts leaves the line out, and the field is then omitted rather
    /// than emitted empty.
    /// </summary>
    public static (string? Description, string Body) SplitOff(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        int newline = text.IndexOf('\n', StringComparison.Ordinal);
        string first = (newline < 0 ? text : text[..newline]).Trim();
        string rest = newline < 0 ? string.Empty : text[(newline + 1)..];

        // A model that bolds the label is still answering the question asked.
        string unmarked = first.TrimStart('*', ' ');
        if (!unmarked.StartsWith(Label, StringComparison.OrdinalIgnoreCase))
        {
            return (null, text);
        }

        string description = unmarked[Label.Length..].Trim();
        if (description.StartsWith("**", StringComparison.Ordinal))
        {
            description = description[2..].Trim();
        }

        // The label line is consumed either way: an empty one is not body text.
        return (description.Length == 0 ? null : description, rest.TrimStart());
    }
}
