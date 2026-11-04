using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Entry points for DocAgent.Knowledge. The API calls each one when its fake flag is false (00-team-overview §6.3).</summary>
public static class KnowledgeServiceCollectionExtensions
{
    /// <summary>
    /// Trajche (Features:Fakes:Detection): register each keyed <c>ISystemDetector</c> ("rules", "rag", "hybrid")
    /// together with its <c>StrategyOption</c>.
    /// </summary>
    public static IServiceCollection AddDocAgentDetection(this IServiceCollection services, IConfiguration configuration)
        => services;

    /// <summary>Trajche (Features:Fakes:DocumentIntelligence): register the Document Intelligence <c>IDocumentTextExtractor</c>.</summary>
    public static IServiceCollection AddDocAgentDocumentIntelligence(this IServiceCollection services, IConfiguration configuration)
        => services;

    /// <summary>
    /// Trajche and Todor (Features:Fakes:Knowledge): Trajche registers the <c>IKnowledgeIngestor</c>; Todor registers
    /// the <c>IKnowledgeRetriever</c> and each keyed <c>ISectionContextProvider</c> ("none", "prestep", "tool",
    /// "agentic") with its <c>StrategyOption</c>.
    /// </summary>
    public static IServiceCollection AddDocAgentKnowledge(this IServiceCollection services, IConfiguration configuration)
        => services;

    /// <summary>Todor (Features:Fakes:Chat): register the streaming <c>IRunChat</c>.</summary>
    public static IServiceCollection AddDocAgentChat(this IServiceCollection services, IConfiguration configuration)
        => services;
}
