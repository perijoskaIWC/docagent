using DocAgent.Core;
using DocAgent.Infrastructure;

namespace Microsoft.Extensions.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Elena: persistence services. The DbContext itself is registered by the API with Aspire's
    /// <c>AddNpgsqlDbContext</c>, which also adds its health check, retries and tracing.
    /// </summary>
    public static IServiceCollection AddDocAgentInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IRunReader, EfRunReader>();
        return services;
    }
}
