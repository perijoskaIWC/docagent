using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Entry point for DocAgent.Safety. The API calls it when its fake flag is false (00-team-overview §6.3).</summary>
public static class SafetyServiceCollectionExtensions
{
    /// <summary>Elena (Features:Fakes:Safety): register the Content Safety <c>IContentScreen</c>.</summary>
    public static IServiceCollection AddDocAgentSafety(this IServiceCollection services, IConfiguration configuration)
        => services;
}
