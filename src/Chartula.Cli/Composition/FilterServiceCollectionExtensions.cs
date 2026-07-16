using Chartula.Cli.Configuration;
using Chartula.Core.Filtering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for filtering. Reads the <c>Chartula:Filter</c> section into
/// <see cref="ChangeFilterRules"/>; the pipeline depends only on
/// <see cref="IChangeFilter"/>. Requires the categorizer and label policy from
/// <see cref="CurationServiceCollectionExtensions"/> and
/// <see cref="LabelServiceCollectionExtensions"/>.
/// </summary>
internal static class FilterServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaFilter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        FilterOptions options = configuration.GetSection(FilterOptions.SectionName).Get<FilterOptions>()
                                ?? new FilterOptions();

        services.AddSingleton(ChangeFilterRules.From(options.ExcludeCategories));
        services.AddSingleton<IChangeFilter, ChangeFilter>();
        return services;
    }
}
