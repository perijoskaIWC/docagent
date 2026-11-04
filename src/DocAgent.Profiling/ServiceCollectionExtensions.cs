using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Entry points for DocAgent.Profiling (00-team-overview §6.3).</summary>
public static class ProfilingServiceCollectionExtensions
{
    /// <summary>
    /// Riste (Features:Fakes:Profiler): register the <c>IExportProfiler</c>, the <c>IDesignPartValidator</c> and every
    /// <c>IDesignCheck</c>.
    /// </summary>
    public static IServiceCollection AddDocAgentProfiling(this IServiceCollection services, IConfiguration configuration)
        => services;

    /// <summary>Riste: register the drill-down tools. Always called; there is no fake for the tools.</summary>
    public static IServiceCollection AddDocAgentTools(this IServiceCollection services, IConfiguration configuration)
        => services;
}
