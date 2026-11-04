using DocAgent.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DocAgent.Fakes;

/// <summary>
/// Runs the real DI registrations in order (fakes or real ones, whatever the flags say) and reports every status,
/// with a short pause between steps so the UI has something to show. Replaced by Boban's MAF workflow when
/// Features:Fakes:Pipeline is false.
/// </summary>
public sealed class FakeRunPipeline(
    IContentScreen screen,
    IExportProfiler profiler,
    ITemplateProvider templates,
    IDesignRenderer renderer,
    IServiceProvider services) : IRunPipeline
{
    private static readonly TimeSpan StepDelay = TimeSpan.FromMilliseconds(400);

    public async Task<DesignResult> RunAsync(DesignRequest request, IProgress<RunProgress>? progress = null, CancellationToken ct = default)
    {
        void Report(RunStatus status, string message, int iteration = 0) =>
            progress?.Report(new RunProgress(request.RunId, status, message, iteration));

        Report(RunStatus.Screening, "Screening the uploaded files");
        var screening = await screen.ScreenAsync(request.Pack, ct);
        if (!screening.Allowed)
            throw new InvalidOperationException($"Content screening blocked the pack: {string.Join(", ", screening.Flags)}");
        await Task.Delay(StepDelay, ct);

        Report(RunStatus.Profiling, $"Profiling {request.Pack.Files.Count} file(s)");
        var model = await profiler.ProfileAsync(request.Pack, ct);
        await Task.Delay(StepDelay, ct);

        Report(RunStatus.Detecting, request.SystemOverride is null
            ? $"Detecting the system with the \"{request.Strategy.Detector}\" detector"
            : $"Using the system chosen by the user: {request.SystemOverride}");
        // The key comes from the request, so keyed services are resolved here, not injected in the constructor.
        var detection = request.SystemOverride is { Length: > 0 } systemId
            ? Override(systemId)
            : await services.GetRequiredKeyedService<ISystemDetector>(request.Strategy.Detector).DetectAsync(model, ct);
        model = await profiler.EnrichAsync(model, request.Pack, detection, ct);
        await Task.Delay(StepDelay, ct);

        Report(RunStatus.Drafting, $"Drafting with the \"{request.Strategy.Author}\" author");
        var template = await templates.GetAsync(request.TemplateId, ct);
        var author = services.GetRequiredKeyedService<IDesignAuthor>(request.Strategy.Author);
        var authoring = await author.AuthorAsync(new AuthoringContext(request, template, model, detection), progress, ct);
        await Task.Delay(StepDelay, ct);

        Report(RunStatus.Reviewing, $"{authoring.Reviews.Count} review(s)", authoring.Reviews.Count);
        await Task.Delay(StepDelay, ct);

        // The worker renders and stores the Markdown itself. Rendering here makes a broken renderer fail the run
        // at the Rendering step, where it belongs.
        Report(RunStatus.Rendering, "Rendering the design");
        var markdown = renderer.ToMarkdown(authoring.Design, template);
        await Task.Delay(StepDelay, ct);

        Report(RunStatus.Completed, $"Completed: {authoring.Design.Parts.Count} section(s), {markdown.Length} characters of Markdown");
        return new DesignResult(request.RunId, request.Strategy, screening, model, detection,
            authoring.Design, authoring.Reviews, authoring.Usage);
    }

    private static SystemDetection Override(string systemId) =>
        new(systemId, DisplayName: systemId, Confidence: 1.0, Evidence: ["Chosen by the user"],
            Alternatives: [], Strategy: "override");
}
