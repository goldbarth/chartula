using Chartula.Core.Review;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for review mode. Reads the <c>Chartula:Review</c> section into
/// <see cref="ReviewOptions"/> (off by default) and registers the coordinator. The
/// default reviewer approves automatically; the interactive console reviewer ships
/// with the CLI command surface.
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

        services.AddSingleton(options);
        services.AddSingleton<IReviewer, AutoApproveReviewer>();
        services.AddSingleton<IReviewCoordinator, ReviewCoordinator>();
        return services;
    }
}
