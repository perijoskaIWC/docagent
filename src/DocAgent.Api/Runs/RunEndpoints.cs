using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using DocAgent.Api.Uploads;
using DocAgent.Core;
using DocAgent.Infrastructure;
using DocAgent.Infrastructure.Entities;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static DocAgent.Api.ApiResults;
using static DocAgent.Api.ApiText;

namespace DocAgent.Api.Runs;

/// <summary>The /api/runs endpoints (guide 1 §4.3).</summary>
internal static class RunEndpoints
{
    private const int MaxReviewLoopsLimit = 5;
    // Shorter than the fake pipeline's 400 ms steps, so no step falls between two reads. Real steps take far longer.
    private static readonly TimeSpan EventPollInterval = TimeSpan.FromMilliseconds(250);

    public static void MapRunEndpoints(this IEndpointRouteBuilder api)
    {
        var runs = api.MapGroup("/runs").WithTags("Runs");
        runs.MapPost("", CreateAsync);
        runs.MapGet("", ListAsync);
        runs.MapGet("/{id:guid}", GetAsync);
        runs.MapGet("/{id:guid}/model", GetModelAsync);
        runs.MapGet("/{id:guid}/design.md", GetMarkdownAsync);
        runs.MapGet("/{id:guid}/events", EventsAsync);
        runs.MapPost("/{id:guid}/rerun", RerunAsync);
        runs.MapPost("/{id:guid}/feedback", FeedbackAsync);
    }

    /// <summary>POST /api/runs: save the run and its files as Queued, hand the id to the worker, answer 202.</summary>
    private static async Task<IResult> CreateAsync(
        HttpRequest request, DocAgentDbContext db, Channel<Guid> queue, ITemplateProvider templates,
        [FromServices] IEnumerable<StrategyOption> strategies, IConfiguration configuration, CancellationToken ct)
    {
        if (!request.HasFormContentType)
            return Invalid("files", "Send the files as multipart/form-data.");

        // Kestrel stops reading a request at about 28 MB, and 30 files of 10 MB need more. The limit has to be raised
        // before the form is read, which is why this endpoint reads the form itself instead of binding IFormFileCollection.
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } sizeLimit)
            sizeLimit.MaxRequestBodySize = UploadLimits.MaxRequestBytes;
        if (await TryReadFormAsync(request, ct) is not { } form)
            return Invalid("files", "The upload is too large, or isn't valid multipart/form-data.");

        string? Field(string name) => NullIfBlank(form[name].ToString());
        var errors = new Dictionary<string, string[]>();

        var defaults = DefaultStrategy(configuration);
        var strategy = new RunStrategy(
            Field("detector") ?? defaults.Detector,
            Field("author") ?? defaults.Author,
            Field("retrieval") ?? defaults.Retrieval,
            ParseBool(Field("lessons"), "lessons", errors) ?? defaults.Lessons,
            ParseInt(Field("maxReviewLoops"), "maxReviewLoops", errors) ?? defaults.MaxReviewLoops);
        var templateId = Field("templateId");
        var target = Field("target");
        await ValidateAsync(templateId, target, strategy, templates, strategies, errors, ct);

        UploadedPack? pack = null;
        try
        {
            pack = await PackUpload.ReadAsync(form.Files, ct);
        }
        catch (InvalidUploadException ex)
        {
            errors["files"] = [ex.Message];
        }

        if (errors.Count > 0 || pack is null || templateId is null)
            return Invalid(errors);

        var run = NewRun(pack.Name, templateId, target, Field("brief"), Field("systemOverride"), strategy,
            pack.Files.Select(file => (file.Path, file.Kind, file.Content)));
        return await QueueAsync(run, db, queue, ct);
    }

    /// <summary>GET /api/runs: the latest 50, newest first, optionally filtered.</summary>
    private static async Task<IResult> ListAsync(DocAgentDbContext db, string? templateId, string? author,
        string? retrieval, string? detector, string? experimentId, CancellationToken ct)
    {
        var query = db.Runs.AsNoTracking();
        if (templateId is not null) query = query.Where(r => r.TemplateId == templateId);
        if (author is not null) query = query.Where(r => r.Author == author);
        if (retrieval is not null) query = query.Where(r => r.Retrieval == retrieval);
        if (detector is not null) query = query.Where(r => r.Detector == detector);
        if (experimentId is not null) query = query.Where(r => r.ExperimentId == experimentId);

        var runs = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .Select(r => new RunSummary(r.Id, r.CreatedAt, r.Status, r.PackName, r.TemplateId,
                new RunStrategy(r.Detector, r.Author, r.Retrieval, r.Lessons, r.MaxReviewLoops),
                r.DetectedSystemId, r.DetectionConfidence, r.InputTokens, r.OutputTokens, r.EstimatedCostUsd,
                r.ExperimentId, r.SourceRunId))
            .ToListAsync(ct);
        return TypedResults.Ok(runs);
    }

    /// <summary>GET /api/runs/{id}: everything about one run except the file contents and the model.</summary>
    private static async Task<IResult> GetAsync(Guid id, DocAgentDbContext db, CancellationToken ct)
    {
        var run = await db.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null)
            return RunNotFound(id);

        var files = await db.RunFiles.AsNoTracking().Where(f => f.RunId == id)
            .Select(f => new RunFileInfo(f.Path, f.Kind, f.SizeBytes)).ToListAsync(ct);
        // The model can be large and has its own endpoint.
        var result = await db.RunResults.AsNoTracking().Where(r => r.RunId == id)
            .Select(r => new { r.ScreeningJson, r.DetectionJson, r.DesignJson, r.Markdown }).FirstOrDefaultAsync(ct);
        var reviews = await db.RunReviews.AsNoTracking().Where(r => r.RunId == id).OrderBy(r => r.Iteration).ToListAsync(ct);

        return TypedResults.Ok(new RunDetails(
            run.Id, run.CreatedAt, run.CompletedAt,
            run.Status, run.ProgressMessage, run.Iteration, run.ErrorMessage,
            run.PackName, run.TemplateId, run.Target, run.Brief, run.SystemOverride, RunMapping.ToStrategy(run),
            [.. files.OrderBy(f => f.Path, StringComparer.Ordinal)],
            result is null ? null : DocAgentJson.Deserialize<ScreeningResult>(result.ScreeningJson),
            result is null ? null : DocAgentJson.Deserialize<SystemDetection>(result.DetectionJson),
            result is null ? null : DocAgentJson.Deserialize<SolutionDesign>(result.DesignJson),
            result?.Markdown,
            [.. reviews.Select(RunMapping.ToReviewResult)],
            RunMapping.ToUsage(run),
            run.TraceId, run.ExperimentId, run.SourceRunId));
    }

    /// <summary>GET /api/runs/{id}/model: the SystemModel JSON.</summary>
    private static async Task<IResult> GetModelAsync(Guid id, DocAgentDbContext db, CancellationToken ct)
    {
        var json = await db.RunResults.Where(r => r.RunId == id).Select(r => r.ModelJson).FirstOrDefaultAsync(ct);
        // Written with DocAgentJson, whose settings match the API's, so it goes out as stored.
        return json is not null
            ? TypedResults.Text(json, "application/json", Encoding.UTF8)
            : await NoResultAsync(db, id, ct);
    }

    /// <summary>GET /api/runs/{id}/design.md: the Markdown as a download named after the design title.</summary>
    private static async Task<IResult> GetMarkdownAsync(Guid id, DocAgentDbContext db, CancellationToken ct)
    {
        var row = await db.RunResults.Where(r => r.RunId == id)
            .Select(r => new { r.DesignJson, r.Markdown }).FirstOrDefaultAsync(ct);
        if (row is null)
            return await NoResultAsync(db, id, ct);

        var title = DocAgentJson.Deserialize<SolutionDesign>(row.DesignJson).Title;
        return TypedResults.File(Encoding.UTF8.GetBytes(row.Markdown), "text/markdown; charset=utf-8", MarkdownFileName(title));
    }

    internal static string MarkdownFileName(string title) => (Slug(title) is { Length: > 0 } slug ? slug : "design") + ".md";

    /// <summary>
    /// GET /api/runs/{id}/events: progress as Server-Sent Events, one RunProgress per change, until the run completes
    /// or fails. It reads the run's row a few times a second, so it shows exactly what GET /api/runs/{id} shows; a
    /// step shorter than the interval can be skipped.
    /// </summary>
    private static async Task<IResult> EventsAsync(Guid id, DocAgentDbContext db, CancellationToken ct)
    {
        if (!await db.Runs.AnyAsync(r => r.Id == id, ct))
            return RunNotFound(id);
        return TypedResults.ServerSentEvents(WatchAsync(db, id, ct));
    }

    private static async IAsyncEnumerable<RunProgress> WatchAsync(DocAgentDbContext db, Guid id,
        [EnumeratorCancellation] CancellationToken ct)
    {
        RunProgress? last = null;
        while (true)
        {
            var current = await db.Runs.AsNoTracking().Where(r => r.Id == id)
                .Select(r => new RunProgress(r.Id, r.Status, r.ProgressMessage, r.Iteration))
                .FirstOrDefaultAsync(ct);
            if (current is null)
                yield break;
            if (current != last)                  // records compare by value, so only changes go out
                yield return last = current;
            if (current.Status is RunStatus.Completed or RunStatus.Failed)
                yield break;
            await Task.Delay(EventPollInterval, ct);
        }
    }

    /// <summary>POST /api/runs/{id}/rerun: a new run on a copy of the same files, with SourceRunId pointing back.</summary>
    private static async Task<IResult> RerunAsync(Guid id, RerunRequest? body, DocAgentDbContext db, Channel<Guid> queue,
        ITemplateProvider templates, [FromServices] IEnumerable<StrategyOption> strategies, CancellationToken ct)
    {
        var source = await db.Runs.AsNoTracking().Include(r => r.Files).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (source is null)
            return RunNotFound(id);

        body ??= new RerunRequest();   // no body: the same settings again, e.g. to measure variance
        var strategy = new RunStrategy(
            body.Detector ?? source.Detector,
            body.Author ?? source.Author,
            body.Retrieval ?? source.Retrieval,
            body.Lessons ?? source.Lessons,
            body.MaxReviewLoops ?? source.MaxReviewLoops);
        var templateId = body.TemplateId ?? source.TemplateId;
        var target = body.Target is null ? source.Target : NullIfBlank(body.Target);
        var systemOverride = body.SystemOverride is null ? source.SystemOverride : NullIfBlank(body.SystemOverride);

        var errors = new Dictionary<string, string[]>();
        await ValidateAsync(templateId, target, strategy, templates, strategies, errors, ct);
        if (errors.Count > 0)
            return Invalid(errors);

        var run = NewRun(source.PackName, templateId, target, source.Brief, systemOverride, strategy,
            source.Files.Select(file => (file.Path, file.Kind, file.Content)));
        run.SourceRunId = source.Id;
        return await QueueAsync(run, db, queue, ct);
    }

    /// <summary>POST /api/runs/{id}/feedback: a thumbs up or down, for the design or one section.</summary>
    private static async Task<IResult> FeedbackAsync(Guid id, FeedbackRequest body, DocAgentDbContext db, CancellationToken ct)
    {
        if (body.Rating is not (-1 or 1))
            return Invalid("rating", "Use 1 (good) or -1 (bad).");
        if (!await db.Runs.AnyAsync(r => r.Id == id, ct))
            return RunNotFound(id);

        db.Feedback.Add(new Feedback
        {
            Id = Guid.CreateVersion7(),
            RunId = id,
            CreatedAt = DateTimeOffset.UtcNow,
            Rating = body.Rating,
            SectionId = NullIfBlank(body.SectionId),
            Comment = NullIfBlank(body.Comment),
        });
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Saved before the id goes into the channel: if the API stops in between, the run is still Queued in the database
    /// and the worker picks it up at the next start.
    /// </summary>
    private static async Task<IResult> QueueAsync(Run run, DocAgentDbContext db, Channel<Guid> queue, CancellationToken ct)
    {
        db.Runs.Add(run);
        await db.SaveChangesAsync(ct);
        await queue.Writer.WriteAsync(run.Id, ct);
        return TypedResults.Accepted($"/api/runs/{run.Id}", new RunCreated(run.Id));
    }

    private static Run NewRun(string packName, string templateId, string? target, string? brief, string? systemOverride,
        RunStrategy strategy, IEnumerable<(string Path, FileKind Kind, byte[] Content)> files)
    {
        // Version 7 GUIDs start with a timestamp, so new rows go to the end of the primary key index instead of
        // landing at random places in it.
        var id = Guid.CreateVersion7();
        return new Run
        {
            Id = id,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = RunStatus.Queued,
            ProgressMessage = "Waiting to start",
            PackName = packName,
            TemplateId = templateId,
            Target = target,
            Brief = brief,
            SystemOverride = systemOverride,
            Detector = strategy.Detector,
            Author = strategy.Author,
            Retrieval = strategy.Retrieval,
            Lessons = strategy.Lessons,
            MaxReviewLoops = strategy.MaxReviewLoops,
            Files = [.. files.Select(file => new RunFile
            {
                Id = Guid.CreateVersion7(),
                RunId = id,
                Path = file.Path,
                Kind = file.Kind,
                SizeBytes = file.Content.LongLength,
                Content = file.Content,
            })],
        };
    }

    /// <summary>The checks create and rerun share. Adds one message per broken field.</summary>
    private static async Task ValidateAsync(string? templateId, string? target, RunStrategy strategy,
        ITemplateProvider templates, IEnumerable<StrategyOption> strategies, Dictionary<string, string[]> errors,
        CancellationToken ct)
    {
        var template = templateId is null ? null : (await templates.ListAsync(ct)).FirstOrDefault(t => t.Id == templateId);
        if (templateId is null)
            errors["templateId"] = ["Choose a template."];
        else if (template is null)
            errors["templateId"] = [$"Unknown template '{templateId}'. GET /api/templates lists them."];
        else if (template.RequiresTarget && target is null)
            errors["target"] = [$"A {template.Name} needs a target, for example Dataverse."];

        // Only strategies that are registered: the same list GET /api/strategies shows.
        var known = strategies.ToLookup(option => option.Slot, option => option.Key);
        CheckKey("detector", strategy.Detector);
        CheckKey("author", strategy.Author);
        CheckKey("retrieval", strategy.Retrieval);
        if (strategy.MaxReviewLoops is < 0 or > MaxReviewLoopsLimit)
            errors["maxReviewLoops"] = [$"Use a whole number from 0 to {MaxReviewLoopsLimit}."];

        void CheckKey(string slot, string key)
        {
            if (!known[slot].Contains(key))
                errors[slot] = [$"Unknown {slot} '{key}'. GET /api/strategies lists them."];
        }
    }

    /// <summary>Strategy:Default from configuration (00-team-overview §8); a missing key keeps RunStrategy's default.</summary>
    private static RunStrategy DefaultStrategy(IConfiguration configuration)
    {
        var section = configuration.GetSection("Strategy:Default");
        var fallback = new RunStrategy();
        return fallback with
        {
            Detector = section["Detector"] ?? fallback.Detector,
            Author = section["Author"] ?? fallback.Author,
            Retrieval = section["Retrieval"] ?? fallback.Retrieval,
        };
    }

    /// <summary>404 when the run doesn't exist; 409 while it exists but has no result (still running, or failed).</summary>
    private static async Task<IResult> NoResultAsync(DocAgentDbContext db, Guid id, CancellationToken ct)
    {
        var run = await db.Runs.Where(r => r.Id == id).Select(r => new { r.Status }).FirstOrDefaultAsync(ct);
        return run is null
            ? RunNotFound(id)
            : TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "No result",
                detail: $"Run {id} is {run.Status}. Only completed runs have a result.");
    }

    private static bool? ParseBool(string? value, string field, Dictionary<string, string[]> errors)
    {
        if (value is null)
            return null;
        if (bool.TryParse(value, out var result))
            return result;
        errors[field] = ["Use true or false."];
        return null;
    }

    private static int? ParseInt(string? value, string field, Dictionary<string, string[]> errors)
    {
        if (value is null)
            return null;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;
        errors[field] = [$"Use a whole number from 0 to {MaxReviewLoopsLimit}."];
        return null;
    }
}
