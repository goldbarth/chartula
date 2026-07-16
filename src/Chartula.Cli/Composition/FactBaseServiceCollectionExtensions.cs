using Chartula.Core.Facts;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for the fact base. The builder is pure domain logic; the
/// pipeline depends only on <see cref="IFactBaseBuilder"/>. Requires the curation,
/// label, and filter services to be registered first.
/// </summary>
internal static class FactBaseServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaFactBase(this IServiceCollection services)
    {
        services.AddSingleton<IFactBaseBuilder, FactBaseBuilder>();
        return services;
    }
}
