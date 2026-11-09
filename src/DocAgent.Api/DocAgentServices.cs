using DocAgent.Agents.Rendering;
using DocAgent.Core;
using DocAgent.Fakes;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Everything a run is made of (guide 1 §4.1): for each Features:Fakes flag, either the fake or the owner's entry point
/// (00-team-overview §6.3), plus the parts that are always real. Hosting (the run queue, the worker, OpenAPI) stays in
/// Program.cs, so the eval harness (guide 2) can call this and run with exactly the API's registrations.
/// </summary>
public static class DocAgentServiceCollectionExtensions
{
    public static IServiceCollection AddDocAgentServices(this IServiceCollection services, IConfiguration configuration)
    {
        var fakes = configuration.GetSection("Features:Fakes");
        bool Fake(string name) => fakes.GetValue(name, true);   // a missing flag means the fake
        var s = services;
        var cfg = configuration;

        if (Fake("Safety"))   s.AddSingleton<IContentScreen, FakeContentScreen>();
        else                  s.AddDocAgentSafety(cfg);

        if (Fake("Profiler")) { s.AddSingleton<IExportProfiler, FakeExportProfiler>();
                                s.AddSingleton<IDesignPartValidator, FakeDesignPartValidator>(); }
        else                  s.AddDocAgentProfiling(cfg);

        if (Fake("Templates")) s.AddSingleton<ITemplateProvider, FakeTemplateProvider>();
        else                   s.AddDocAgentTemplates(cfg);

        if (Fake("Detection")) { s.AddKeyedSingleton<ISystemDetector>(KeyedService.AnyKey, (_, key) => new FakeSystemDetector((string)key!));
                                 s.AddFakeStrategyOptions("detector", "rules", "rag", "hybrid"); }
        else                   s.AddDocAgentDetection(cfg);

        if (Fake("DocumentIntelligence")) s.AddSingleton<IDocumentTextExtractor, FakeDocumentTextExtractor>();
        else                              s.AddDocAgentDocumentIntelligence(cfg);

        if (Fake("Knowledge")) { s.AddSingleton<IKnowledgeRetriever, FakeKnowledgeRetriever>();
                                 s.AddSingleton<IKnowledgeIngestor, FakeKnowledgeIngestor>();
                                 s.AddKeyedSingleton<ISectionContextProvider>(KeyedService.AnyKey, (_, key) => new FakeSectionContextProvider((string)key!));
                                 s.AddFakeStrategyOptions("retrieval", "none", "prestep", "tool"); }
        else                   s.AddDocAgentKnowledge(cfg);

        if (Fake("Authoring")) { s.AddKeyedSingleton<IDesignAuthor>(KeyedService.AnyKey, (_, key) => new FakeDesignAuthor((string)key!));
                                 s.AddFakeStrategyOptions("author", "single-agent", "specialists", "concurrent"); }
        else                   s.AddDocAgentAuthors(cfg);

        if (Fake("Pipeline")) s.AddSingleton<IRunPipeline, FakeRunPipeline>();
        else                  s.AddDocAgentPipeline(cfg);

        if (Fake("Chat"))     s.AddSingleton<IRunChat, FakeRunChat>();
        else                  s.AddDocAgentChat(cfg);

        s.AddDocAgentTools(cfg);                                    // Riste: drill-down tools, always real
        s.AddSingleton<IDesignRenderer, MarkdownDesignRenderer>();   // Riste may move this into AddDocAgentTemplates
        return services;
    }
}
