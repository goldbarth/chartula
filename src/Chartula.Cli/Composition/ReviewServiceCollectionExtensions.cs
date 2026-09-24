using Chartula.Core.Review;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for review mode. Reads the <c>Chartula:Review</c> section into
/// <see cref="ReviewOptions"/> (off by default) and registers the coordinator.
/// No interactive reviewer exists yet, so review mode on is refused: the only reviewer,
/// <see cref="AutoApproveReviewer"/>, would approve every text unseen.
/// </summary>
internal static class ReviewServiceCollectionExtensions
{
    private const string SectionName = "Chartula:Review";

    public static IServiceCollection AddChartulaReview(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ReviewOptions options = configuration.GetSection(SectionName).Get<ReviewOptions>()
                                ?? new ReviewOptions();

        if (options.Enabled)
        {
            throw new InvalidOperationException(
                "review.enabled is not available yet: no interactive reviewer exists, " +
                "so every text would be approved unseen. Remove it or set it to false.");
        }

        services.AddSingleton(options);
        services.AddSingleton<IReviewer, AutoApproveReviewer>();
        services.AddSingleton<IReviewCoordinator, ReviewCoordinator>();
        return services;
    }
}
