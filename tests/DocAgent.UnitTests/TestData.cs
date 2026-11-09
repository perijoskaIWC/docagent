using DocAgent.Core;
using DocAgent.Fakes;
using Microsoft.Extensions.Configuration;
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

    /// <summary>
    /// The API's own registrations (guide 1 §4.1). With no configuration every Features:Fakes flag defaults to true,
    /// so this is the all-fakes setup, and a change to the API's wiring is tested here too.
    /// </summary>
    public static ServiceProvider FakeServices(Action<IServiceCollection>? configure = null) =>
        Services(new Dictionary<string, string?>(), configure);

    /// <summary>The API's registrations with the given configuration, e.g. { ["Features:Fakes:Chat"] = "false" }.</summary>
    public static ServiceProvider Services(IDictionary<string, string?> settings, Action<IServiceCollection>? configure = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var s = new ServiceCollection().AddDocAgentServices(configuration);
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
