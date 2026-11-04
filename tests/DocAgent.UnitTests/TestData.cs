using DocAgent.Agents.Rendering;
using DocAgent.Core;
using DocAgent.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace DocAgent.UnitTests;

/// <summary>Synthetic test inputs and the DI setup the API uses when every fake flag is on.</summary>
internal static class TestData
{
    public const string WorkOrdersCsv = "id,technician_id,status\r\n1,7,open\r\n2,9,closed\r\n";

    public static ExportPack Pack() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"), "helio-full",
        [
            new ExportFile("data/Work-Orders.csv", FileKind.Csv, WorkOrdersCsv.Length, WorkOrdersCsv, Bytes: null),
            new ExportFile("openapi.yaml", FileKind.OpenApi, 14, "openapi: 3.0.0", Bytes: null),
        ]);

    public static ExportPack EmptyPack() => new(Guid.NewGuid(), "empty", []);

    public static DesignRequest Request(RunStrategy? strategy = null, string? systemOverride = null) =>
        new(Guid.NewGuid(), Pack(), "system-assessment", Target: null, Brief: null, strategy ?? new RunStrategy(), systemOverride);

    /// <summary>An authoring context for the system-assessment template, built from the fakes.</summary>
    public static async Task<AuthoringContext> AuthoringContextAsync(ExportPack pack)
    {
        var template = await new FakeTemplateProvider().GetAsync("system-assessment");
        var model = await new FakeExportProfiler().ProfileAsync(pack);
        var detection = await new FakeSystemDetector("rules").DetectAsync(model);
        var request = new DesignRequest(Guid.NewGuid(), pack, template.Id, Target: null, Brief: null, new RunStrategy());
        return new AuthoringContext(request, template, model, detection);
    }

    /// <summary>The fake registrations from guide 1 §4.1 with every Features:Fakes flag on.</summary>
    public static ServiceProvider FakeServices(Action<IServiceCollection>? configure = null)
    {
        var s = new ServiceCollection();
        s.AddSingleton<IContentScreen, FakeContentScreen>();
        s.AddSingleton<IExportProfiler, FakeExportProfiler>();
        s.AddSingleton<IDesignPartValidator, FakeDesignPartValidator>();
        s.AddSingleton<ITemplateProvider, FakeTemplateProvider>();
        s.AddKeyedSingleton<ISystemDetector>(KeyedService.AnyKey, (_, key) => new FakeSystemDetector((string)key!));
        s.AddFakeStrategyOptions("detector", "rules", "rag", "hybrid");
        s.AddSingleton<IDocumentTextExtractor, FakeDocumentTextExtractor>();
        s.AddSingleton<IKnowledgeRetriever, FakeKnowledgeRetriever>();
        s.AddSingleton<IKnowledgeIngestor, FakeKnowledgeIngestor>();
        s.AddKeyedSingleton<ISectionContextProvider>(KeyedService.AnyKey, (_, key) => new FakeSectionContextProvider((string)key!));
        s.AddFakeStrategyOptions("retrieval", "none", "prestep", "tool");
        s.AddKeyedSingleton<IDesignAuthor>(KeyedService.AnyKey, (_, key) => new FakeDesignAuthor((string)key!));
        s.AddFakeStrategyOptions("author", "single-agent", "specialists", "concurrent");
        s.AddSingleton<IRunPipeline, FakeRunPipeline>();
        s.AddSingleton<IRunChat, FakeRunChat>();
        s.AddSingleton<IDesignRenderer, MarkdownDesignRenderer>();
        configure?.Invoke(s);
        return s.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}

/// <summary>
/// Records progress synchronously, in order. The built-in Progress&lt;T&gt; posts each report to the thread pool,
/// so the order it calls back in isn't guaranteed.
/// </summary>
internal sealed class RecordingProgress<T> : IProgress<T>
{
    private readonly List<T> _items = [];

    public IReadOnlyList<T> Items => _items;

    public void Report(T value) => _items.Add(value);
}
