using System.Text;

namespace Chartula.Core.Review;

/// <summary>
/// Formats a <see cref="ReviewItem"/> for a reviewer: the generated text followed
/// by the flagged passages, so a maintainer can see what needs a closer look
/// before approving.
/// </summary>
public static class ReviewPresentation
{
    public static string Format(ReviewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        StringBuilder builder = new();
        builder.Append("=== ").Append(item.Audience).AppendLine(" ===");
        builder.AppendLine(item.Text);

        if (item.Flags.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Flagged for review:");
            foreach (string flag in item.Flags)
            {
                builder.Append("  ! ").AppendLine(flag);
            }
        }

        return builder.ToString().TrimEnd();
    }
}
