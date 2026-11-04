using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Entry points for DocAgent.Agents. The API calls each one when its fake flag is false (00-team-overview §6.3).</summary>
public static class AgentsServiceCollectionExtensions
{
    /// <summary>Riste (Features:Fakes:Templates): register the <c>ITemplateProvider</c> that reads the template files.</summary>
    public static IServiceCollection AddDocAgentTemplates(this IServiceCollection services, IConfiguration configuration)
        => services;

    /// <summary>
    /// Boban and Stevcho (Features:Fakes:Authoring): register each keyed <c>IDesignAuthor</c> ("single-agent", then
    /// "specialists", "concurrent", "group-chat") together with its <c>StrategyOption</c>.
    /// </summary>
    public static IServiceCollection AddDocAgentAuthors(this IServiceCollection services, IConfiguration configuration)
        => services;

    /// <summary>Boban (Features:Fakes:Pipeline): register the <c>IRunPipeline</c> MAF workflow.</summary>
    public static IServiceCollection AddDocAgentPipeline(this IServiceCollection services, IConfiguration configuration)
        => services;
}
