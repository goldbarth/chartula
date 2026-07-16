using Chartula.Cli.Configuration;
using Chartula.Core.Categorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for category presentation. Reads the <c>Chartula:Categories</c>
/// section into <see cref="CategorySettings"/> (order, display names, breaking
/// prominence); the generator picks it up to order and name the facts it renders.
/// </summary>
internal static class CategoryServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaCategories(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        CategoryOptions options = configuration.GetSection(CategoryOptions.SectionName).Get<CategoryOptions>()
                                  ?? new CategoryOptions();

        services.AddSingleton(CategorySettings.From(options.Order, options.Names, options.BreakingProminent));
        return services;
    }
}
