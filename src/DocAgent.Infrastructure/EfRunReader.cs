using DocAgent.Core;
using DocAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocAgent.Infrastructure;

/// <summary>
/// Read access to finished runs for the drill-down tools, the MCP server and the Ask chat. A singleton that opens a
/// scope per call, because a DbContext is scoped and several of its consumers are singletons.
/// </summary>
internal sealed class EfRunReader(IServiceScopeFactory scopes) : IRunReader
{
    public async Task<DesignResult?> GetAsync(Guid runId, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DocAgentDbContext>();

        var run = await db.Runs.AsNoTracking()
            .Include(r => r.Result)
            .Include(r => r.Reviews)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run?.Result is not { } result)
            return null;    // no such run, or it hasn't finished

        return new DesignResult(
            run.Id,
            new RunStrategy(run.Detector, run.Author, run.Retrieval, run.Lessons, run.MaxReviewLoops),
            DocAgentJson.Deserialize<ScreeningResult>(result.ScreeningJson),
            DocAgentJson.Deserialize<SystemModel>(result.ModelJson),
            DocAgentJson.Deserialize<SystemDetection>(result.DetectionJson),
            DocAgentJson.Deserialize<SolutionDesign>(result.DesignJson),
            [.. run.Reviews.OrderBy(r => r.Iteration).Select(ToReviewResult)],
            new RunUsage(run.InputTokens, run.OutputTokens, run.ModelCalls, run.ToolCalls, run.EstimatedCostUsd));
    }

    public async Task<ExportPack?> GetPackAsync(Guid runId, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DocAgentDbContext>();

        var run = await db.Runs.AsNoTracking()
            .Include(r => r.Files)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);
        return run is null ? null : ExportPackMapping.ToExportPack(run);
    }

    private static ReviewResult ToReviewResult(RunReview review) =>
        new(review.Iteration, review.Approved, review.Score,
            DocAgentJson.Deserialize<List<ReviewIssue>>(review.IssuesJson),
            DocAgentJson.Deserialize<List<CheckResult>>(review.ChecksJson),
            review.Summary);
}
