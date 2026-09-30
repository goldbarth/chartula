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
        IConfigurationSection section = configuration.GetSection(FilterOptions.SectionName);
        FilterOptions options = section.Get<FilterOptions>() ?? new FilterOptions();

        // The binder reads an empty list as no list at all, which keeps the default.
        // Only this list's default differs from empty, so only here does [] need reading.
        List<string>? excluded = options.ExcludeCategories
                                 ?? (section[nameof(FilterOptions.ExcludeCategories)] == ChartulaYamlReader.EmptyList ? [] : null);

        services.AddSingleton(ChangeFilterRules.From(excluded));
        services.AddSingleton<IChangeFilter, ChangeFilter>();
        return services;
    }
}
