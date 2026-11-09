using System.Diagnostics;
using System.Threading.Channels;
using DocAgent.Core;
using DocAgent.Infrastructure;
using DocAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocAgent.Api.Runs;

/// <summary>
/// Runs queued designs in the background, one at a time (guide 1 §4.2). POST /api/runs saves a run as Queued and writes
/// its id to the channel; this worker builds the DesignRequest from the database, calls the pipeline and stores the
/// result. The services are resolved per run from a new scope: the DbContext is scoped, and an owner's real
/// implementation may be too.
/// </summary>
internal sealed class RunWorker(Channel<Guid> queue, IServiceScopeFactory scopes, ILogger<RunWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverAsync(stoppingToken);

        await foreach (var runId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(runId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;   // the API is stopping; the next start marks this run as interrupted
            }
            catch (Exception ex)
            {
                // A failure outside the pipeline, such as the database being down. Keep serving the queue.
                logger.LogError(ex, "Run {RunId} could not be processed", runId);
            }
        }
    }

    /// <summary>
    /// After a restart. A run that was mid-pipeline can't be resumed, so it fails with a message that says so; runs
    /// still waiting go back in the queue, oldest first. A run created during this scan can end up in the channel
    /// twice, which is harmless: ProcessAsync skips any run that is no longer Queued.
    /// </summary>
    private async Task RecoverAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DocAgentDbContext>();

        var interrupted = await db.Runs
            .Where(r => r.Status != RunStatus.Queued && r.Status != RunStatus.Completed && r.Status != RunStatus.Failed)
            .ExecuteUpdateAsync(set => set
                .SetProperty(r => r.Status, RunStatus.Failed)
                .SetProperty(r => r.ProgressMessage, "Interrupted")
                .SetProperty(r => r.ErrorMessage, "The API stopped while this run was in progress. Rerun it.")
                .SetProperty(r => r.CompletedAt, DateTimeOffset.UtcNow), ct);

        var queued = await db.Runs.Where(r => r.Status == RunStatus.Queued)
            .OrderBy(r => r.CreatedAt).Select(r => r.Id).ToListAsync(ct);
        foreach (var runId in queued)
            await queue.Writer.WriteAsync(runId, ct);

        if (interrupted > 0 || queued.Count > 0)
            logger.LogInformation("Startup: {Interrupted} interrupted run(s) marked as failed, {Queued} queued run(s) resumed",
                interrupted, queued.Count);
    }

    private async Task ProcessAsync(Guid runId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<DocAgentDbContext>();

        var run = await db.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is not { Status: RunStatus.Queued })
            return;   // already processed (queued twice at startup) or deleted
        var files = await db.RunFiles.AsNoTracking().Where(f => f.RunId == runId).ToListAsync(ct);
        var request = new DesignRequest(run.Id, ExportPackMapping.ToExportPack(run.Id, run.PackName, files),
            run.TemplateId, run.Target, run.Brief, RunMapping.ToStrategy(run), run.SystemOverride);

        // The root span of the run: what the pipeline does (model calls, tools, queries) nests under it.
        using var activity = Telemetry.Source.StartActivity("docagent.run");
        activity?.SetTag("run.id", run.Id.ToString());
        activity?.SetTag("strategy.detector", request.Strategy.Detector);
        activity?.SetTag("strategy.author", request.Strategy.Author);
        activity?.SetTag("strategy.retrieval", request.Strategy.Retrieval);
        activity?.SetTag("template.id", request.TemplateId);
        if (activity is not null)
        {
            var traceId = activity.TraceId.ToHexString();
            await db.Runs.Where(r => r.Id == runId).ExecuteUpdateAsync(set => set.SetProperty(r => r.TraceId, traceId), ct);
        }

        await using var progress = new RunProgressWriter(runId, scopes, logger);
        try
        {
            var result = await services.GetRequiredService<IRunPipeline>().RunAsync(request, progress, ct);
            await progress.DisposeAsync();   // every progress update is written before the final status

            var template = await services.GetRequiredService<ITemplateProvider>().GetAsync(request.TemplateId, ct);
            var markdown = services.GetRequiredService<IDesignRenderer>().ToMarkdown(result.Design, template);
            await SaveResultAsync(db, runId, result, markdown, ct);
            logger.LogInformation("Run {RunId} completed", runId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            await progress.DisposeAsync();
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Run {RunId} failed", runId);
            await SaveFailureAsync(db, runId, ex.Message, ct);
        }
    }

    /// <summary>
    /// The result, the reviews and the final status in one SaveChanges, which is one transaction: nobody can read a
    /// Completed run that has no design.
    /// </summary>
    private static async Task SaveResultAsync(DocAgentDbContext db, Guid runId, DesignResult result, string markdown, CancellationToken ct)
    {
        // Loaded again, tracked: the progress writer has changed the row since the untracked read at the start.
        var run = await db.Runs.SingleAsync(r => r.Id == runId, ct);
        run.Status = RunStatus.Completed;
        run.CompletedAt = DateTimeOffset.UtcNow;
        run.ProgressMessage = $"Completed: {result.Design.Parts.Count} section(s)";
        run.Iteration = result.Reviews.Count > 0 ? result.Reviews.Max(review => review.Iteration) : 0;
        run.DetectedSystemId = result.Detection.SystemId;
        run.DetectionConfidence = result.Detection.Confidence;
        run.InputTokens = result.Usage.InputTokens;
        run.OutputTokens = result.Usage.OutputTokens;
        run.ModelCalls = result.Usage.ModelCalls;
        run.ToolCalls = result.Usage.ToolCalls;
        run.EstimatedCostUsd = result.Usage.EstimatedCostUsd;

        db.RunResults.Add(new RunResult
        {
            RunId = runId,
            ScreeningJson = DocAgentJson.Serialize(result.Screening),
            ModelJson = DocAgentJson.Serialize(result.Model),
            DetectionJson = DocAgentJson.Serialize(result.Detection),
            DesignJson = DocAgentJson.Serialize(result.Design),
            Markdown = markdown,
        });
        db.RunReviews.AddRange(result.Reviews.Select(review => RunMapping.ToRunReview(runId, review)));
        await db.SaveChangesAsync(ct);
    }

    private static Task SaveFailureAsync(DocAgentDbContext db, Guid runId, string error, CancellationToken ct) =>
        db.Runs.Where(r => r.Id == runId).ExecuteUpdateAsync(set => set
            .SetProperty(r => r.Status, RunStatus.Failed)
            .SetProperty(r => r.ProgressMessage, "Failed")
            .SetProperty(r => r.ErrorMessage, error)
            .SetProperty(r => r.CompletedAt, DateTimeOffset.UtcNow), ct);
}
